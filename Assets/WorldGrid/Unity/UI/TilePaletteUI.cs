using System.Collections.Generic;
using UnityEngine;
using WorldGrid.Unity.Assets;
using WorldGrid.Runtime.Tiles;

namespace WorldGrid.Unity.UI
{
    [DisallowMultipleComponent]
    public sealed class TilePaletteUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TileLibraryAsset tileLibraryAsset;
        [SerializeField] private TileBrushState brushState;

        [Header("UI")]
        [SerializeField] private Transform contentRoot;
        [SerializeField] private TilePaletteTileButton tileButtonPrefab;

        [Header("Options")]
        [SerializeField] private bool showTileIdLabel = true;

        private readonly List<TilePaletteTileButton> _buttons = new();

        private void Awake()
        {
            if (tileLibraryAsset == null ||
                brushState == null ||
                contentRoot == null ||
                tileButtonPrefab == null)
            {
                UnityEngine.Debug.LogError("TilePaletteUI: Missing references.", this);
                enabled = false;
                return;
            }

            if (tileLibraryAsset.atlasTexture == null)
            {
                UnityEngine.Debug.LogError("TilePaletteUI: TileLibraryAsset atlasTexture is null.", this);
                enabled = false;
                return;
            }

            Rebuild();
        }

        private void OnEnable()
        {
            if (brushState != null)
                brushState.OnSelectionChanged += HandleSelectionChanged;

            // Make sure visuals match current selection when enabled
            HandleSelectionChanged(brushState != null ? brushState.selectedTileId : -1);
        }

        private void OnDisable()
        {
            if (brushState != null)
                brushState.OnSelectionChanged -= HandleSelectionChanged;
        }

        private void HandleSelectionChanged(int selectedTileId)
        {
            foreach (var btn in _buttons)
            {
                if (btn != null)
                    btn.SetSelected(btn.TileId == selectedTileId);
            }
        }

        public void Rebuild()
        {
            ClearButtons();

            var atlas = tileLibraryAsset.atlasTexture;
            int atlasW = atlas.width;
            int atlasH = atlas.height;

            var entries = tileLibraryAsset.entries;
            if (entries == null || entries.Count == 0)
                return;

            foreach (var e in entries)
            {
                if (e == null) continue;

                RectUv uv = e.overrideUv
                    ? new RectUv(e.uvMin.x, e.uvMin.y, e.uvMax.x, e.uvMax.y)
                    : TileLibraryAsset.ComputeUvFromTileCoord(
                        atlasW, atlasH,
                        tileLibraryAsset.tilePixelSize,
                        tileLibraryAsset.paddingPixels,
                        tileLibraryAsset.originTopLeft,
                        e.tileCoord,
                        e.tileSpan
                    );

                string label = showTileIdLabel
                    ? e.tileId.ToString()
                    : (string.IsNullOrWhiteSpace(e.name) ? $"tile_{e.tileId}" : e.name);

                var btn = Instantiate(tileButtonPrefab, contentRoot);
                _buttons.Add(btn);

                // Assumes your TilePaletteTileButton has a Bind method like this:
                // Bind(int tileId, Texture2D atlas, RectUv uv, string label, System.Action<int> onClicked)
                btn.Bind(
                    tileId: e.tileId,
                    atlas: atlas,
                    uv: uv,
                    label: label,
                    onClicked: OnTileClicked
                );

                btn.SetSelected(brushState.selectedTileId == e.tileId);
            }
        }

        private void OnTileClicked(int tileId)
        {
            // Preserve your prior behavior: clicking selects; clicking same tile again deselects (optional).
            if (brushState.selectedTileId == tileId)
                brushState.selectedTileId = -1;
            else
                brushState.selectedTileId = tileId;
        }

        private void ClearButtons()
        {
            foreach (var b in _buttons)
            {
                if (b != null)
                    Destroy(b.gameObject);
            }
            _buttons.Clear();
        }
    }
}
