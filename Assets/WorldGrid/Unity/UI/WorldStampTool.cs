using UnityEngine;
using UnityEngine.EventSystems;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.World;
using WorldGrid.Unity.Input;
using WorldGrid.Unity.UI;

namespace WorldGrid.Unity.Tilemap
{
    /// <summary>
    /// World stamping tool driven by TileBrushState (selectedTileId / eraseTileId / brushRadius).
    /// Replaces WorldDebugPaintTool selection logic with a UI-driven brush.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldStampTool : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private WorldHost worldHost;
        [SerializeField] private WorldPointer2D pointer;
        [SerializeField] private TileBrushState brushState;

        [Header("Mode")]
        [SerializeField] private bool enableStamping = true;

        [Header("Rate Limiting")]
        [Tooltip("Minimum time between writes while staying on the same cell.")]
        [SerializeField] private float minWriteIntervalSeconds = 0.02f;

        [Tooltip("If true, always write immediately when the hovered cell changes.")]
        [SerializeField] private bool writeOnCellChange = true;

        private float _nextAllowedWriteTime;
        private bool _hasLastCell;
        private CellCoord _lastCell;

        private void Awake()
        {
            if (worldHost == null || pointer == null || brushState == null)
            {
                UnityEngine.Debug.LogError("WorldStampTool: Missing required references.", this);
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            if (!enabled || !enableStamping)
                return;

            // If clicking UI (palette), do not stamp into the world.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            var hit = pointer.CurrentHit;
            if (!hit.Valid)
            {
                _hasLastCell = false;
                return;
            }

            bool paintHeld = UnityEngine.Input.GetMouseButton(0);
            bool eraseHeld = UnityEngine.Input.GetMouseButton(1);

            if (!paintHeld && !eraseHeld)
            {
                _hasLastCell = false;
                return;
            }

            int targetTileId = paintHeld ? brushState.selectedTileId : brushState.eraseTileId;

            bool cellChanged = !_hasLastCell || hit.Cell != _lastCell;

            if (writeOnCellChange && cellChanged)
            {
                ApplyBrush(hit.Cell, targetTileId);
                _lastCell = hit.Cell;
                _hasLastCell = true;
                _nextAllowedWriteTime = Time.time + Mathf.Max(0f, minWriteIntervalSeconds);
                return;
            }

            if (Time.time >= _nextAllowedWriteTime)
            {
                ApplyBrush(hit.Cell, targetTileId);
                _lastCell = hit.Cell;
                _hasLastCell = true;
                _nextAllowedWriteTime = Time.time + Mathf.Max(0f, minWriteIntervalSeconds);
            }
        }

        private void ApplyBrush(CellCoord center, int tileId)
        {
            SparseChunkWorld world = worldHost.World;
            if (world == null)
                return;

            int r = Mathf.Max(0, brushState.brushRadius);

            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = center.X + dx;
                    int y = center.Y + dy;
                    world.SetTile(x, y, tileId);
                }
            }
        }
    }
}
