using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGrid.Runtime.Tiles;
using WorldGrid.Unity.Rendering;

namespace WorldGrid.Unity.UI
{
    [DisallowMultipleComponent]
    public sealed class TilePaletteUI : MonoBehaviour
    {
        #region Inspector

        [Header("Refs")]
        [SerializeField] private WorldHost worldHost;
        [SerializeField] private TileBrushState brushState;

        [Header("Tile Library Selection")]
        [SerializeField] private TileLibraryKey tileLibraryKey;

        [Header("UI")]
        [SerializeField] private Transform contentRoot;
        [SerializeField] private TilePaletteTileButton tileButtonPrefab;

        [Tooltip("Optional central tooltip UI (shown on hover).")]
        [SerializeField] private TilePaletteTooltipUI tooltipUI;

        #endregion

        #region State

        private readonly List<TilePaletteTileButton> _buttons = new();

        private ITileLibrarySource _tileSource;
        private ITileLibraryView _tileView;

        private bool _initialized;

        private static Texture2D _white1x1;

        #endregion

        #region Public Read-Only Accessors (for spawner injection)

        public TileBrushState BrushState => brushState;
        public TileLibraryKey TileLibraryKey => tileLibraryKey;

        #endregion

        #region Unity Lifecycle

        public void SetWorld(WorldHost host)
        {
            worldHost = host;

            if (isActiveAndEnabled && !_initialized)
            {
                if (tryInitialize())
                {
                    _initialized = true;
                    Rebuild();
                    subscribe();
                }
            }
        }

        private void Awake()
        {
            if (brushState == null || contentRoot == null || tileButtonPrefab == null)
            {
                UnityEngine.Debug.LogError("TilePaletteUI disabled: Missing required UI references.", this);
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            if (worldHost == null)
                return;

            if (!_initialized)
            {
                if (!tryInitialize())
                {
                    enabled = false;
                    return;
                }

                _initialized = true;
                Rebuild();
            }

            subscribe();
        }

        private void OnDisable()
        {
            unsubscribe();

            // Hide tooltip when panel is disabled (prevents “stuck tooltip”).
            if (tooltipUI != null)
                tooltipUI.Hide();
        }

        #endregion

        #region Public API

        public void Rebuild()
        {
            if (_tileView == null || _tileView.Library == null)
                return;

            clearButtons();

            var lib = _tileView.Library;
            var atlas = _tileView.AtlasTexture != null ? _tileView.AtlasTexture : getWhite1x1();

            foreach (var def in lib.EnumerateDefs())
            {
                if (def == null)
                    continue;

                var btn = Instantiate(tileButtonPrefab, contentRoot);
                _buttons.Add(btn);

                var uv = getTileUv(def);
                var tint = getTileTint(lib, def.TileId);

                btn.Bind(def, atlas, uv, onTileClicked, tint, tooltipUI);
                btn.SetSelected(def.TileId == brushState.selectedTileId);
            }
        }

        #endregion

        #region Initialization

        private bool tryInitialize()
        {
            _tileSource = findTileLibrarySource(worldHost);
            if (_tileSource == null)
            {
                UnityEngine.Debug.LogError("TilePaletteUI disabled: No component on WorldHost implements ITileLibrarySource.", this);
                return false;
            }

            if (tileLibraryKey.IsEmpty || !_tileSource.Has(tileLibraryKey))
            {
                UnityEngine.Debug.LogError($"TilePaletteUI disabled: Tile library key '{tileLibraryKey}' not found on provider.", this);
                return false;
            }

            if (!tryResolveView(tileLibraryKey, out _tileView))
            {
                UnityEngine.Debug.LogError($"TilePaletteUI disabled: Provider returned invalid view for key '{tileLibraryKey}'.", this);
                return false;
            }

            if (_tileView.AtlasTexture == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"TilePaletteUI: View for key '{tileLibraryKey}' has no AtlasTexture. Using fallback swatches (tinted 1x1).",
                    this);
            }

            return true;
        }

        private bool tryResolveView(TileLibraryKey key, out ITileLibraryView view)
        {
            view = null;

            if (_tileSource is TileLibraryProvider provider)
            {
                return provider.TryGet(key, out view, out _);
            }

            try
            {
                view = _tileSource.Get(key);
            }
            catch
            {
                view = null;
            }

            return view != null && view.Library != null;
        }

        private static ITileLibrarySource findTileLibrarySource(WorldHost host)
        {
            if (host == null)
                return null;

            var behaviours = host.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ITileLibrarySource src)
                    return src;
            }

            return null;
        }

        #endregion

        #region Selection

        private void subscribe()
        {
            if (brushState != null)
                brushState.OnSelectionChanged += handleSelectionChanged;
        }

        private void unsubscribe()
        {
            if (brushState != null)
                brushState.OnSelectionChanged -= handleSelectionChanged;
        }

        private void handleSelectionChanged(int selectedTileId)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                var b = _buttons[i];
                if (b != null)
                    b.SetSelected(b.TileId == selectedTileId);
            }
        }

        private void onTileClicked(int tileId)
        {
            brushState.selectedTileId = (brushState.selectedTileId == tileId) ? -1 : tileId;
        }

        #endregion

        #region Button Build Helpers

        private RectUv getTileUv(TileDef def)
        {
            return _tileView.AtlasTexture != null ? def.Uv : new RectUv(0f, 0f, 1f, 1f);
        }

        private static Color32 getTileTint(TileLibrary lib, int tileId)
        {
            var tint = new Color32(255, 255, 255, 255);

            if (lib.TryGetProperty<TileColorProperty>(tileId, out var cp) && cp != null)
                tint = cp.Color;

            return tint;
        }

        #endregion

        #region Cleanup

        private void clearButtons()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] != null)
                    Destroy(_buttons[i].gameObject);
            }

            _buttons.Clear();
        }

        private static Texture2D getWhite1x1()
        {
            if (_white1x1 != null)
                return _white1x1;

            _white1x1 = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "WorldGrid_White1x1"
            };

            _white1x1.SetPixel(0, 0, Color.white);
            _white1x1.Apply(false, false);
            return _white1x1;
        }

        #endregion
    }
}
