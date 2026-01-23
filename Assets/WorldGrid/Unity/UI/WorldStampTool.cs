using UnityEngine;
using UnityEngine.EventSystems;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.World;
using WorldGrid.Unity.Input;
using WorldGrid.Unity.UI;

namespace WorldGrid.Unity.Tilemap
{
    /// <summary>
    /// World stamping tool driven by TileBrushState.
    /// Handles paint/erase input and writes tileIds into the SparseChunkWorld.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldStampTool : MonoBehaviour
    {
        #region Inspector References

        [Header("References")]
        [SerializeField] private WorldHost worldHost;
        [SerializeField] private WorldPointer2D pointer;
        [SerializeField] private TileBrushState brushState;

        #endregion

        #region Stamping Mode

        [Header("Mode")]
        [SerializeField] private bool enableStamping = true;

        #endregion

        #region Rate Limiting

        [Header("Rate Limiting")]
        [Tooltip("Minimum time between writes while staying on the same cell.")]
        [SerializeField] private float minWriteIntervalSeconds = 0.02f;

        [Tooltip("If true, always write immediately when the hovered cell changes.")]
        [SerializeField] private bool writeOnCellChange = true;

        #endregion

        #region Selection Rules

        [Header("Selection")]
        [Tooltip("TileIds < 0 mean 'no brush selected'.")]
        [SerializeField] private int noSelectionTileId = -1;

        #endregion

        #region State

        private float _nextAllowedWriteTime;
        private bool _hasLastCell;
        private CellCoord _lastCell;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (!validateReferences())
            {
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            if (!shouldProcessFrame())
                return;

            if (handleCancelInput())
                return;

            if (isPointerOverUi())
                return;

            if (!tryGetTargetCell(out var cell))
                return;

            if (!tryGetStampIntent(out var tileId))
                return;

            processStamp(cell, tileId);
        }

        #endregion

        #region Frame Guards

        private bool validateReferences()
        {
            if (worldHost == null || pointer == null || brushState == null)
            {
                UnityEngine.Debug.LogError("WorldStampTool: Missing required references.", this);
                return false;
            }

            return true;
        }

        private bool shouldProcessFrame()
        {
            if (!enabled || !enableStamping)
                return false;

            return true;
        }

        private bool handleCancelInput()
        {
            if (!UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                return false;

            brushState.selectedTileId = noSelectionTileId;
            _hasLastCell = false;
            return true;
        }

        private bool isPointerOverUi()
        {
            if (EventSystem.current == null)
                return false;

            return EventSystem.current.IsPointerOverGameObject();
        }

        #endregion

        #region Input Resolution

        private bool tryGetTargetCell(out CellCoord cell)
        {
            cell = default;

            var hit = pointer.CurrentHit;
            if (!hit.Valid)
            {
                _hasLastCell = false;
                return false;
            }

            cell = hit.Cell;
            return true;
        }

        private bool tryGetStampIntent(out int tileId)
        {
            tileId = 0;

            bool paintHeld = UnityEngine.Input.GetMouseButton(0);
            bool eraseHeld = UnityEngine.Input.GetMouseButton(1);

            if (!paintHeld && !eraseHeld)
            {
                _hasLastCell = false;
                return false;
            }

            if (paintHeld && brushState.selectedTileId < 0)
            {
                _hasLastCell = false;
                return false;
            }

            tileId = paintHeld
                ? brushState.selectedTileId
                : brushState.eraseTileId;

            return true;
        }

        #endregion

        #region Stamping Logic

        private void processStamp(CellCoord cell, int tileId)
        {
            bool cellChanged = !_hasLastCell || cell != _lastCell;

            if (writeOnCellChange && cellChanged)
            {
                applyBrush(cell, tileId);
                recordWrite(cell);
                return;
            }

            if (Time.time >= _nextAllowedWriteTime)
            {
                applyBrush(cell, tileId);
                recordWrite(cell);
            }
        }

        private void recordWrite(CellCoord cell)
        {
            _lastCell = cell;
            _hasLastCell = true;
            _nextAllowedWriteTime = Time.time + Mathf.Max(0f, minWriteIntervalSeconds);
        }

        #endregion

        #region World Write

        private void applyBrush(CellCoord center, int tileId)
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

        #endregion
    }
}
