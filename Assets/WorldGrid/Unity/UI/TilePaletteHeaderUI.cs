using TMPro;
using UnityEngine;
using WorldGrid.Runtime.Tiles;
using WorldGrid.Unity.Rendering;

namespace WorldGrid.Unity.UI
{
    /// <summary>
    /// Palette header/status presenter.
    /// Independent of palette button/grid UI.
    ///
    /// Reads from:
    /// - TileBrushState (selection + brush radius)
    /// - ITileLibraryView (atlas name + tile defs)
    ///
    /// No Update loop; refreshes only when notified.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TilePaletteHeaderUI : MonoBehaviour
    {
        [Header("Sources")]
        [Tooltip("Selection + brush radius source.")]
        [SerializeField] private TileBrushState brushState;

        [Tooltip("Tile library view source (atlas + library).")]
        [SerializeField] private MonoBehaviour tileViewSource; // must implement ITileLibraryView

        [Tooltip("Optional: shown as the primary title line. If empty, title line is hidden.")]
        [SerializeField] private string libraryKeyLabel;

        [Header("Text")]
        [SerializeField] private TMP_Text titleText;        // library key label
        [SerializeField] private TMP_Text subtitleText;     // atlas/texture name
        [SerializeField] private TMP_Text tileCountText;    // "N tiles" (optional)
        [SerializeField] private TMP_Text selectedText;     // "Selected: ..."
        [SerializeField] private TMP_Text brushSizeText;    // "Brush: NxN"

        [Header("No Selection UX (Optional)")]
        [SerializeField] private CanvasGroup headerCanvasGroup;
        [SerializeField, Range(0f, 1f)] private float noSelectionAlpha = 0.55f;

        [Header("Debug (Optional)")]
        [SerializeField] private bool showUvInSelected = false;

        private ITileLibraryView _tileView;

        // cheap caches to avoid reassigning same strings repeatedly
        private string _lastTitle;
        private string _lastSubtitle;
        private string _lastTileCount;
        private string _lastSelected;
        private string _lastBrush;
        private float _lastHeaderAlpha = -1f;

        private void Awake()
        {
            _tileView = tileViewSource as ITileLibraryView;
        }

        private void OnEnable()
        {
            if (brushState != null)
                brushState.OnSelectionChanged += handleSelectionChanged;

            Refresh();
        }

        private void OnDisable()
        {
            if (brushState != null)
                brushState.OnSelectionChanged -= handleSelectionChanged;
        }

        private void handleSelectionChanged(int _)
        {
            RefreshSelectionAndBrush();
        }

        /// <summary>
        /// Assign/update sources at runtime (e.g., after prefab spawn).
        /// Call once after you resolve the actual view from world/host.
        /// </summary>
        public void Bind(TileBrushState brush, ITileLibraryView view, string libraryKey = null)
        {
            // Unhook old
            if (brushState != null)
                brushState.OnSelectionChanged -= handleSelectionChanged;

            brushState = brush;
            _tileView = view;
            tileViewSource = view as MonoBehaviour;

            if (libraryKey != null)
                libraryKeyLabel = libraryKey;

            // Hook new
            if (brushState != null && isActiveAndEnabled)
                brushState.OnSelectionChanged += handleSelectionChanged;

            Refresh();
        }

        /// <summary>
        /// Refresh everything (context + selection + brush).
        /// Use after changing the bound tile view / library key.
        /// </summary>
        public void Refresh()
        {
            RefreshContext();
            RefreshSelectionAndBrush();
        }

        /// <summary>
        /// Refresh title/atlas/tile count. No selection logic.
        /// </summary>
        public void RefreshContext()
        {
            // Title: library key (optional)
            if (titleText != null)
            {
                bool hasTitle = !string.IsNullOrWhiteSpace(libraryKeyLabel);
                titleText.gameObject.SetActive(hasTitle);

                if (hasTitle)
                    setText(titleText, ref _lastTitle, libraryKeyLabel);
                else
                    _lastTitle = null;
            }

            // Subtitle: atlas/texture name (if available)
            var atlas = _tileView != null ? _tileView.AtlasTexture : null;

            if (subtitleText != null)
            {
                if (atlas != null)
                {
                    subtitleText.gameObject.SetActive(true);
                    setText(subtitleText, ref _lastSubtitle, atlas.name);
                }
                else
                {
                    subtitleText.gameObject.SetActive(false);
                    _lastSubtitle = null;
                }
            }

            // Tile count (optional)
            if (tileCountText != null)
            {
                int count = getTileCount(_tileView);
                if (count >= 0)
                {
                    tileCountText.gameObject.SetActive(true);
                    setText(tileCountText, ref _lastTileCount, $"{count} tiles");
                }
                else
                {
                    tileCountText.gameObject.SetActive(false);
                    _lastTileCount = null;
                }
            }
        }

        /// <summary>
        /// Refresh selection line, brush size line, and "no selection" visuals.
        /// </summary>
        public void RefreshSelectionAndBrush()
        {
            // Brush size
            if (brushSizeText != null)
            {
                int r = brushState != null ? brushState.brushRadius : 0;
                int n = (r * 2) + 1;
                setText(brushSizeText, ref _lastBrush, $"Brush: {n}x{n}");
            }

            // Selection
            int selectedId = brushState != null ? brushState.selectedTileId : -1;
            bool hasSel = selectedId >= 0;

            if (selectedText != null)
            {
                string line = hasSel
                    ? buildSelectedLine(_tileView, selectedId, showUvInSelected)
                    : "Selected: None";

                setText(selectedText, ref _lastSelected, line);
            }

            // Muted header when no selection
            if (headerCanvasGroup != null)
            {
                float targetAlpha = hasSel ? 1f : noSelectionAlpha;
                if (_lastHeaderAlpha < 0f || !Mathf.Approximately(_lastHeaderAlpha, targetAlpha))
                {
                    headerCanvasGroup.alpha = targetAlpha;
                    _lastHeaderAlpha = targetAlpha;
                }
            }
        }

        private static int getTileCount(ITileLibraryView view)
        {
            var lib = view != null ? view.Library : null;
            if (lib == null)
                return -1;

            // EnumerateDefs() returns dictionary values; this should be allocation-free.
            // (IEnumerable itself may allocate depending on implementation; for v1 this is fine.
            // If you later want zero-GC strictness, cache this count when the palette rebuilds.)
            int count = 0;
            foreach (var def in lib.EnumerateDefs())
            {
                if (def != null)
                    count++;
            }
            return count;
        }

        private static string buildSelectedLine(ITileLibraryView view, int tileId, bool includeUv)
        {
            string label = tileId.ToString();
            RectUv uv = default;
            bool hasUv = false;

            var lib = view != null ? view.Library : null;
            if (lib != null && lib.TryGetDef(tileId, out var def) && def != null)
            {
                if (!string.IsNullOrWhiteSpace(def.Name))
                    label = def.Name;

                uv = def.Uv;
                hasUv = true;
            }

            if (!includeUv || !hasUv)
                return $"Selected: {label}";

            return $"Selected: {label}  UV=({uv.UMin:F2},{uv.VMin:F2},{uv.UMax:F2},{uv.VMax:F2})";
        }

        private static void setText(TMP_Text t, ref string cache, string value)
        {
            if (t == null)
                return;

            if (cache == value)
                return;

            cache = value;
            t.text = value;
        }
    }
}
