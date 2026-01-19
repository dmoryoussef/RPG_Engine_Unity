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
            if (_tileView == null || _tileView.Library == null || _tileView.AtlasTexture == null)
            {
                UnityEngine.Debug.LogError($"TilePaletteUI: Provider returned invalid view for key '{tileLibraryKey}'.", this);
                enabled = false;
                return;
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

            var atlas = _tileView.AtlasTexture;
            var lib = _tileView.Library;

            foreach (var def in lib.EnumerateDefs())
            {
                if (def == null) continue;

                var btn = Instantiate(tileButtonPrefab, contentRoot);
                _buttons.Add(btn);

                string label = showTileIdLabel ? def.TileId.ToString() : def.Name;

                btn.Bind(
                    tileId: def.TileId,
                    atlas: atlas,
                    uv: def.Uv,
                    label: label,
                    onClicked: OnTileClicked
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
            // Unity can't GetComponent<Interface>(), so scan MonoBehaviours.
            var behaviours = host.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ITileLibrarySource src)
                    return src;
            }
            return null;
        }
    }
}
