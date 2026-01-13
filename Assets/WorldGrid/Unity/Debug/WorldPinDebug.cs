using UnityEngine;
using WorldGrid.Runtime.Coords;
using WorldGrid.Unity;
using WorldGrid.Unity.Input;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WorldGrid.Unity.Debug
{
    /// <summary>
    /// Click-to-pin debug tool.
    /// LMB pins the current hovered cell.
    /// RMB clears (or Backspace).
    ///
    /// Uses WorldPointer2D as the sole source of truth for picking.
    /// Does not mutate the world.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldPinDebug : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private WorldHost worldHost;
        [SerializeField] private WorldPointer2D pointer;

        [Header("Input")]
        [SerializeField] private bool enablePinning = true;
        [SerializeField] private bool clearWithBackspace = true;

        [Header("Draw")]
        [SerializeField] private bool drawPinnedOutline = true;
        [SerializeField] private bool drawMouseToPinLine = false;

        [Tooltip("Z used for gizmo lines (world space).")]
        [SerializeField] private float gizmoZ = -0.05f;

#if UNITY_EDITOR
        [Tooltip("Outline thickness in pixels (editor Handles).")]
        [SerializeField] private float outlineThickness = 4f;

        [Tooltip("Line thickness in pixels (editor Handles).")]
        [SerializeField] private float lineThickness = 2f;
#endif

        [Header("Pinned (Read-Only)")]
        [SerializeField] private bool hasPin;
        [SerializeField] private CellCoord pinnedCell;
        [SerializeField] private ChunkCoord pinnedChunk;
        [SerializeField] private int pinnedLocalX;
        [SerializeField] private int pinnedLocalY;
        [SerializeField] private int pinnedTileId;

        private void Awake()
        {
            if (worldHost == null || pointer == null)
            {
                UnityEngine.Debug.LogError("WorldPinDebug: Missing required references.", this);
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            pointer.PointerDown += OnPointerDown;
        }

        private void OnDisable()
        {
            if (pointer == null)
                return;

            pointer.PointerDown -= OnPointerDown;
        }

        private void Update()
        {
            if (!enabled)
                return;

            if (!enablePinning)
                return;

            if (clearWithBackspace && UnityEngine.Input.GetKeyDown(KeyCode.Backspace))
                ClearPin();
        }

        private void OnPointerDown(WorldPointerHit hit, int button)
        {
            if (!enablePinning)
                return;

            if (button == 0)
            {
                if (!hit.Valid)
                    return;

                SetPin(hit);
                return;
            }

            if (button == 1)
            {
                ClearPin();
            }
        }

        private void SetPin(WorldPointerHit hit)
        {
            hasPin = true;

            pinnedCell = hit.Cell;
            pinnedChunk = hit.Chunk;
            pinnedLocalX = hit.LocalX;
            pinnedLocalY = hit.LocalY;
            pinnedTileId = hit.TileId;
        }

        private void ClearPin()
        {
            hasPin = false;

            pinnedCell = default;
            pinnedChunk = default;
            pinnedLocalX = 0;
            pinnedLocalY = 0;
            pinnedTileId = 0;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!enabled)
                return;

            if (!drawPinnedOutline && !drawMouseToPinLine)
                return;

            if (worldHost == null || pointer == null)
                return;

            if (!hasPin)
                return;

            float cellSize = worldHost.CellSize;
            if (cellSize <= 0f)
                return;

            Transform root = worldHost.WorldRoot;
            if (root == null)
                return;

            // Compute pinned cell corners in worldRoot-local space.
            float x0 = pinnedCell.X * cellSize;
            float y0 = pinnedCell.Y * cellSize;

            Vector3 blL = new Vector3(x0, y0, 0f);
            Vector3 brL = new Vector3(x0 + cellSize, y0, 0f);
            Vector3 trL = new Vector3(x0 + cellSize, y0 + cellSize, 0f);
            Vector3 tlL = new Vector3(x0, y0 + cellSize, 0f);

            Vector3 blW = root.TransformPoint(blL); blW.z = gizmoZ;
            Vector3 brW = root.TransformPoint(brL); brW.z = gizmoZ;
            Vector3 trW = root.TransformPoint(trL); trW.z = gizmoZ;
            Vector3 tlW = root.TransformPoint(tlL); tlW.z = gizmoZ;

            if (drawPinnedOutline)
            {
                Handles.color = Color.yellow;
                Handles.DrawAAPolyLine(
                    Mathf.Max(1f, outlineThickness),
                    new Vector3[] { blW, brW, trW, tlW, blW });
            }

            if (drawMouseToPinLine)
            {
                var hit = pointer.CurrentHit;
                if (hit.Valid)
                {
                    Vector3 pinCenterL = new Vector3(x0 + cellSize * 0.5f, y0 + cellSize * 0.5f, 0f);
                    Vector3 pinCenterW = root.TransformPoint(pinCenterL); pinCenterW.z = gizmoZ;

                    Vector3 mouseW = hit.WorldPoint;
                    mouseW.z = gizmoZ;

                    Handles.color = Color.yellow;
                    Handles.DrawAAPolyLine(
                        Mathf.Max(1f, lineThickness),
                        new Vector3[] { mouseW, pinCenterW });
                }
            }
        }
#endif
    }
}
