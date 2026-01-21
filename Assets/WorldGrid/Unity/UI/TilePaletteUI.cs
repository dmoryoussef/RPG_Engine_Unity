using System.Collections.Generic;
using UnityEngine;
using WorldGrid.Runtime.Tiles;
using WorldGrid.Unity;

namespace WorldGrid.Unity.UI
{
    [DisallowMultipleComponent]
    public sealed class TilePaletteUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private WorldHost worldHost;
        [SerializeField] private TileBrushState brushState;

        [Header("Tile Library Selection")]
        [Tooltip("Which tile library entry to display (e.g. 'world', 'debug', 'interior').")]
        [SerializeField] private TileLibraryKey tileLibraryKey;

        [Header("UI")]
        [SerializeField] private Transform contentRoot;
        [SerializeField] private TilePaletteTileButton tileButtonPrefab;

        [Header("Options")]
        [SerializeField] private bool showTileIdLabel = true;

        private readonly List<TilePaletteTileButton> _buttons = new();

        private ITileLibrarySource _tileSource;
        private ITileLibraryView _tileView;

        // NEW: fallback texture for no-atlas previews
        private static Texture2D s_White1x1;

        private void Awake()
        {
            if (brushState == null || contentRoot == null || tileButtonPrefab == null)
            {
                UnityEngine.Debug.LogError("TilePaletteUI: Missing required UI references.", this);
                enabled = false;
                return;
            }

            if (worldHost == null)
            {
                UnityEngine.Debug.LogError("TilePaletteUI: worldHost not assigned.", this);
                enabled = false;
                return;
            }

            _tileSource = FindTileLibrarySource(worldHost);
            if (_tileSource == null)
            {
                UnityEngine.Debug.LogError("TilePaletteUI: No component on WorldHost implements ITileLibrarySource.", this);
                enabled = false;
                return;
            }

            if (tileLibraryKey.IsEmpty || !_tileSource.Has(tileLibraryKey))
            {
                UnityEngine.Debug.LogError($"TilePaletteUI: Tile library key '{tileLibraryKey}' not found on provider.", this);
                enabled = false;
                return;
            }

            _tileView = _tileSource.Get(tileLibraryKey);
            if (_tileView == null || _tileView.Library == null)
            {
                UnityEngine.Debug.LogError($"TilePaletteUI: Provider returned invalid view for key '{tileLibraryKey}'.", this);
                enabled = false;
                return;
            }

            if (_tileView.AtlasTexture == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"TilePaletteUI: View for key '{tileLibraryKey}' has no AtlasTexture. " +
                    "Using fallback swatches (tinted 1x1).", this);
            }

            Rebuild();
        }

        private void OnEnable()
        {
            if (brushState != null)
                brushState.OnSelectionChanged += HandleSelectionChanged;
        }

        private void OnDisable()
        {
            if (brushState != null)
                brushState.OnSelectionChanged -= HandleSelectionChanged;
        }

        private void HandleSelectionChanged(int selectedTileId)
        {
            for (int i = 0; i < _buttons.Count; i++)
                _buttons[i].SetSelected(_buttons[i].TileId == selectedTileId);
        }

        public void Rebuild()
        {
            ClearButtons();

            var lib = _tileView.Library;

            Texture atlas = _tileView.AtlasTexture != null ? _tileView.AtlasTexture : GetWhite1x1();

            foreach (var def in lib.EnumerateDefs())
            {
                if (def == null) continue;

                var btn = Instantiate(tileButtonPrefab, contentRoot);
                _buttons.Add(btn);

                string label = showTileIdLabel ? def.TileId.ToString() : def.Name;

                // If we have a real atlas, use authored UVs.
                // If not, use full-rect UV on a 1x1 texture and tint via TileColorProperty.
                RectUv uv = _tileView.AtlasTexture != null ? def.Uv : new RectUv(0f, 0f, 1f, 1f);

                // Default tint = white (no effect)
                var tint = new Color32(255, 255, 255, 255);
                if (lib.TryGetProperty<TileColorProperty>(def.TileId, out var cp) && cp != null)
                {
                    tint = cp.Color;
                }

                btn.Bind(
                    tileId: def.TileId,
                    atlas: atlas,
                    uv: uv,
                    label: label,
                    onClicked: OnTileClicked,
                    tint: tint
                );

                btn.SetSelected(def.TileId == brushState.selectedTileId);
            }
        }

        private void OnTileClicked(int tileId)
        {
            brushState.selectedTileId = (brushState.selectedTileId == tileId) ? -1 : tileId;
        }

        private void ClearButtons()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] != null)
                    Destroy(_buttons[i].gameObject);
            }
            _buttons.Clear();
        }

        private static ITileLibrarySource FindTileLibrarySource(WorldHost host)
        {
            var behaviours = host.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ITileLibrarySource src)
                    return src;
            }
            return null;
        }

        private static Texture2D GetWhite1x1()
        {
            if (s_White1x1 != null)
                return s_White1x1;

            s_White1x1 = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "WorldGrid_White1x1"
            };
            s_White1x1.SetPixel(0, 0, Color.white);
            s_White1x1.Apply(false, false);
            return s_White1x1;
        }
    }
}
