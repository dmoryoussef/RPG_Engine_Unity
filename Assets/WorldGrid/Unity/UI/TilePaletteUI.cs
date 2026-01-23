using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGrid.Runtime.Tiles;
using WorldGrid.Unity.Rendering;

namespace WorldGrid.Unity.UI
{
    /// <summary>
    /// Simple palette UI for selecting a tileId from a TileLibraryKey.
    ///
    /// Responsibilities:
    /// - Resolves tile library view from a provider on the WorldHost
    /// - Builds a set of tile buttons
    /// - Updates selection visuals based on TileBrushState
    ///
    /// Lifecycle:
    /// - Resolves provider/view and builds UI in OnEnable (safe for dynamic runtime creation)
    /// - Subscribes/unsubscribes to brush state selection changes
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TilePaletteUI : MonoBehaviour
    {
        #region Inspector

        [Header("Refs")]
        [SerializeField] private WorldHost worldHost;
        [SerializeField] private TileBrushState brushState;

        [Header("Tile Library Selection")]
        [Tooltip("Which tile library entry to display (for example 'world', 'debug', 'interior').")]
        [SerializeField] private TileLibraryKey tileLibraryKey;

        [Header("UI")]
        [SerializeField] private Transform contentRoot;
        [SerializeField] private TilePaletteTileButton tileButtonPrefab;

        [Header("Options")]
        [SerializeField] private bool showTileIdLabel = true;

        #endregion

        #region State

        private readonly List<TilePaletteTileButton> _buttons = new();

        private ITileLibrarySource _tileSource;
        private ITileLibraryView _tileView;

        private bool _initialized;

        private static Texture2D _white1x1;

        #endregion

        #region Unity Lifecycle

        public void SetWorld(WorldHost host) => worldHost = host;

        private void Awake()
        {
            if (!validateRequiredRefs())
            {
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
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

                var label = getTileLabel(def);
                var uv = getTileUv(def);
                var tint = getTileTint(lib, def.TileId);

                btn.Bind(def.TileId, atlas, uv, label, onTileClicked, tint);


                btn.SetSelected(def.TileId == brushState.selectedTileId);
            }
        }

        #endregion

        #region Initialization

        private bool validateRequiredRefs()
        {
            if (brushState == null || contentRoot == null || tileButtonPrefab == null)
            {
                UnityEngine.Debug.LogError("TilePaletteUI disabled: Missing required UI references.", this);
                return false;
            }

            if (worldHost == null)
            {
                UnityEngine.Debug.LogError("TilePaletteUI disabled: worldHost not assigned.", this);
                return false;
            }

            return true;
        }

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

            // Prefer runtime-safe provider method when available.
            if (_tileSource is TileLibraryProvider provider)
            {
                return provider.TryGet(key, out view, out _);
            }

            // Interface fallback: call Get only after Has.
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

        private string getTileLabel(TileDef def)
        {
            return showTileIdLabel ? def.TileId.ToString() : def.Name;
        }

        private RectUv getTileUv(TileDef def)
        {
            // If we have a real atlas, use authored UVs.
            // If not, use full-rect UV on a 1x1 texture and tint via TileColorProperty.
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
