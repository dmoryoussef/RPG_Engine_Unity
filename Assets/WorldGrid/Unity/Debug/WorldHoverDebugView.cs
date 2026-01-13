using Core;
using UnityEngine;
using WorldGrid.Unity.Input;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WorldGrid.Unity.Debug
{
    /// <summary>
    /// Debug-only visualizer for the current hover hit from WorldPointer2D.
    /// Draws tile outline + labels (cell/chunk/local/tileId).
    ///
    /// This script does not handle input or coordinate picking.
    /// It only consumes WorldPointer2D output.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldHoverDebugView : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private WorldPointer2D pointer;

        [Header("Display")]
        [SerializeField] private bool drawTileOutline = true;
        [SerializeField] private bool drawCenterCross = true;
        [SerializeField] private bool drawLabel = true;

        [Tooltip("How large the cross marker is, in cell units.")]
        [SerializeField] private float crossSizeCells = 0.25f;

        [Tooltip("Z offset to ensure gizmos are visible above the world (negative pushes 'back').")]
        [SerializeField] private float gizmoZ = -0.05f;

        [Tooltip("Optional: show an outline even when hit is invalid (at last valid hit).")]
        [SerializeField] private bool keepLastValidWhenInvalid = true;

        private WorldPointerHit _current;
        private WorldPointerHit _lastValid;

        private void Awake()
        {
            if (pointer == null)
            {
                pointer = GetComponent<WorldPointer2D>();
            }

            if (pointer == null)
            {
                UnityEngine.Debug.LogError("WorldHoverDebugView: pointer reference not assigned.", this);
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            pointer.HoverChanged += OnHoverChanged;
            pointer.HoverEntered += OnHoverEntered;
            pointer.HoverExited += OnHoverExited;
            pointer.PointerDown += OnMouseButtonDown;
            _current = pointer.CurrentHit;
            if (_current.Valid)
                _lastValid = _current;
        }

        private void OnDisable()
        {
            if (pointer == null)
                return;

            pointer.HoverChanged -= OnHoverChanged;
            pointer.HoverEntered -= OnHoverEntered;
            pointer.HoverExited -= OnHoverExited;
            pointer.PointerDown -= OnMouseButtonDown;
        }

        private void OnHoverChanged(WorldPointerHit prev, WorldPointerHit next)
        {
            _current = next;
            if (_current.Valid)
                _lastValid = _current;
        }

        private void OnHoverEntered(WorldPointerHit hit)
        {
            _current = hit;
            if (_current.Valid)
                _lastValid = _current;
        }

        private void OnHoverExited(WorldPointerHit hit)
        {
            // Keep _current as invalid to reflect state.
            _current = default;
        }

        private void OnMouseButtonDown(WorldPointerHit hit, int button)
        {
            UnityEngine.Debug.Log($"WorldHoverDebugView: OnMouseButtonDown at Cell {hit.Cell.X},{hit.Cell.Y} TileId {hit.TileId}", this);
        }
 
      

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!enabled)
                return;

            if (pointer == null)
                return;

            var hit = pointer.CurrentHit;

            if (!hit.Valid && keepLastValidWhenInvalid)
                hit = _lastValid;

            if (!hit.Valid)
                return;
            Gizmos.color = Color.green;
            // We draw in the same space as the worldRoot local mapping.
            // The hit.LocalPoint is already in worldRoot-local space.
            float cellSize = GetCellSizeFromPointerOwner();
            if (cellSize <= 0f)
                return;

            Transform root = GetWorldRootFromPointerOwner();
            if (root == null)
                return;

            // Compute the tile's local-space AABB (bottom-left corner at cell * cellSize).
            float x0 = hit.Cell.X * cellSize;
            float y0 = hit.Cell.Y * cellSize;

            Vector3 bl = new Vector3(x0, y0, gizmoZ);
            Vector3 br = new Vector3(x0 + cellSize, y0, gizmoZ);
            Vector3 tr = new Vector3(x0 + cellSize, y0 + cellSize, gizmoZ);
            Vector3 tl = new Vector3(x0, y0 + cellSize, gizmoZ);

            // Convert local corners to world space for Gizmos.
            Vector3 wBL = root.TransformPoint(bl);
            Vector3 wBR = root.TransformPoint(br);
            Vector3 wTR = root.TransformPoint(tr);
            Vector3 wTL = root.TransformPoint(tl);

            if (drawTileOutline)
            {
                Gizmos.DrawLine(wBL, wBR);
                Gizmos.DrawLine(wBR, wTR);
                Gizmos.DrawLine(wTR, wTL);
                Gizmos.DrawLine(wTL, wBL);
            }

            if (drawCenterCross)
            {
                Vector3 cLocal = new Vector3(x0 + cellSize * 0.5f, y0 + cellSize * 0.5f, gizmoZ);
                Vector3 cWorld = root.TransformPoint(cLocal);

                float crossHalf = Mathf.Clamp(crossSizeCells, 0.01f, 1f) * cellSize * 0.5f;

                Gizmos.DrawLine(cWorld + new Vector3(-crossHalf, 0f, 0f), cWorld + new Vector3(crossHalf, 0f, 0f));
                Gizmos.DrawLine(cWorld + new Vector3(0f, -crossHalf, 0f), cWorld + new Vector3(0f, crossHalf, 0f));
            }

            if (drawLabel)
            {
                Handles.color = Color.black;
                Vector3 labelWorld = (wTL + wTR) * 0.5f;

                string text =
                    $"Cell {hit.Cell.X},{hit.Cell.Y}\n" +
                    $"Chunk {hit.Chunk.X},{hit.Chunk.Y}\n" +
                    $"Local {hit.LocalX},{hit.LocalY}\n" +
                    $"TileId {hit.TileId}";

                Handles.Label(labelWorld, text);
            }
        }
#endif

        private float GetCellSizeFromPointerOwner()
        {
            // Pointer reads mapping from WorldHost. We avoid duplicating config here.
            // If this view is used without WorldHost mapping, fall back safely.
            var owner = pointer != null ? pointer.GetComponentInParent<WorldHost>() : null;
            if (owner != null)
                return owner.CellSize;

            //// Fallback: attempt to find on same GameObject (if host is elsewhere).
            //var owner = Registry.GetAll<WorldHost>();
            //if (owner != null)
            //    return owner.CellSize;

            return 1f;
        }

        private Transform GetWorldRootFromPointerOwner()
        {
            var owner = pointer != null ? pointer.GetComponentInParent<WorldHost>() : null;
            if (owner != null)
                return owner.WorldRoot;

            //owner = FindObjectOfType<WorldHost>();
            //if (owner != null)
            //    return owner.WorldRoot;

            return null;
        }
    }
}
