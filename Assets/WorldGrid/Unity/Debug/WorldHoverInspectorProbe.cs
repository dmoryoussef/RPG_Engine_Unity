using UnityEngine;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.Tiles;
using WorldGrid.Unity;
using WorldGrid.Unity.Input;
using WorldGrid.Unity.Assets;

namespace WorldGrid.Unity.Debug
{
    /// <summary>
    /// Inspector-only probe that mirrors the current WorldPointer2D hover hit.
    /// No drawing, no mutation, no coordinate math.
    /// Intended for validation and later promotion to gameplay inspection tools.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldHoverInspectorProbe : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private WorldPointer2D pointer;
        [SerializeField] private WorldHost worldHost;

        [Header("Optional")]
        [SerializeField] private TileLibraryAsset tileLibraryAsset;

        // Runtime
        private TileLibrary _tileLibrary;

        [Header("Hover State (Read-Only)")]
        [SerializeField] private bool hoverValid;
        [SerializeField] private Vector3 worldPoint;

        [SerializeField] private CellCoord cell;
        [SerializeField] private ChunkCoord chunk;
        [SerializeField] private int localX;
        [SerializeField] private int localY;

        [SerializeField] private int tileId;
        [SerializeField] private string tileInfo;

        private void Awake()
        {
            if (pointer == null || worldHost == null)
            {
                UnityEngine.Debug.LogError(
                    "WorldHoverInspectorProbe: Missing required references.",
                    this);
                enabled = false;
                return;
            }

            if (tileLibraryAsset != null)
                _tileLibrary = tileLibraryAsset.BuildRuntime();
        }

        private void OnEnable()
        {
            pointer.HoverChanged += OnHoverChanged;
            pointer.HoverEntered += OnHoverEntered;
            pointer.HoverExited += OnHoverExited;

            UpdateFromHit(pointer.CurrentHit);
        }

        private void OnDisable()
        {
            if (pointer == null)
                return;

            pointer.HoverChanged -= OnHoverChanged;
            pointer.HoverEntered -= OnHoverEntered;
            pointer.HoverExited -= OnHoverExited;
        }

        private void OnHoverChanged(WorldPointerHit prev, WorldPointerHit next)
        {
            UpdateFromHit(next);
        }

        private void OnHoverEntered(WorldPointerHit hit)
        {
            UpdateFromHit(hit);
        }

        private void OnHoverExited(WorldPointerHit hit)
        {
            Clear();
        }

        private void UpdateFromHit(WorldPointerHit hit)
        {
            hoverValid = hit.Valid;

            if (!hoverValid)
            {
                Clear();
                return;
            }

            worldPoint = hit.WorldPoint;

            cell = hit.Cell;
            chunk = hit.Chunk;

            localX = hit.LocalX;
            localY = hit.LocalY;

            tileId = hit.TileId;

            if (_tileLibrary != null)
                tileInfo = _tileLibrary.ToDebugString(tileId);
            else
                tileInfo = string.Empty;
        }

        private void Clear()
        {
            hoverValid = false;

            worldPoint = default;

            cell = default;
            chunk = default;

            localX = 0;
            localY = 0;

            tileId = 0;
            tileInfo = string.Empty;
        }
    }
}
