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

            Rebuild();
        }

        public void Rebuild()
        {
            ClearButtons();

            if (tileLibraryAsset.atlasTexture == null)
            {
                UnityEngine.Debug.LogError("TilePaletteUI: atlasTexture not assigned.", this);
                return;
            }

            var entries = tileLibraryAsset.entries;
            if (entries == null || entries.Count == 0)
                return;

            int atlasW = tileLibraryAsset.atlasTexture.width;
            int atlasH = tileLibraryAsset.atlasTexture.height;

            foreach (var e in entries)
            {
                if (e == null) continue;

                var btn = Instantiate(tileButtonPrefab, contentRoot);
                _buttons.Add(btn);

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

                btn.Bind(
                    tileId: e.tileId,
                    atlas: tileLibraryAsset.atlasTexture,
                    uv: uv,
                    label: label,
                    onClicked: OnTileClicked
                );

                btn.SetSelected(e.tileId == brushState.selectedTileId);
            }
        }

        private void OnTileClicked(int tileId)
        {
            brushState.selectedTileId = tileId;

            foreach (var btn in _buttons)
                btn.SetSelected(btn.TileId == tileId);
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
