using UnityEngine;
using WorldPlacement.Runtime.Grid;
using WorldPlacement.Unity.Assets;
using WorldPlacement.Unity.Host;
using WorldGrid.Unity.Input; // WorldPointer2D

namespace WorldPlacement.Unity.Debug
{
    [DisallowMultipleComponent]
    public sealed class WorldPlacementDebugOverlay : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private WorldPointer2D pointer;
        [SerializeField] private WorldPlacementHost host;
        [SerializeField] private PlacementDefAsset selected;

        [Header("Placement Preview Settings")]
        [SerializeField] private Rotation4 rotation = Rotation4.R0;
        [SerializeField] private bool requireNonDefaultTile = true;

        [Header("Drawing")]
        [SerializeField] private float tileWorldSize = 1f;
        [SerializeField] private Vector3 worldOrigin = Vector3.zero;

        [Tooltip("Z offset so wireframes sit 'in front' of the tilemap visually.")]
        [SerializeField] private float overlayZOffset = -0.05f;

        [SerializeField] private bool drawPlaced = true;
        [SerializeField] private bool drawGhost = true;

        // Cache last valid hover so gizmos don't vanish / stay teal-only
        private bool _hasLastHover;
        private Cell2i _lastHoverCell;

        // Scratch for footprint expansion
        private readonly System.Collections.Generic.List<Cell2i> _scratch =
            new System.Collections.Generic.List<Cell2i>(64);

        private void Update()
        {
            if (pointer == null) return;

            var hit = pointer.CurrentHit;
            if (hit.Valid)
            {
                _lastHoverCell = new Cell2i(hit.Cell.X, hit.Cell.Y);
                _hasLastHover = true;
            }
        }

        private void OnDrawGizmos()
        {
            if (host == null || host.System == null)
                return;

            if (drawPlaced)
                DrawPlacedFootprints();

            if (drawGhost)
                DrawGhostPreview();
        }

        private void DrawGhostPreview()
        {
            if (selected == null)
                return;

            if (!_hasLastHover)
                return;

            var anchor = _lastHoverCell;
            var def = selected.ToRuntime();

            var report = host.System.Evaluate(def, anchor, rotation, requireNonDefaultTile);
            def.Footprint.GetOccupiedWorldCells(anchor, rotation, _scratch);

            // Ghost color: green allowed, red blocked
            Gizmos.color = report.Allowed
                ? new Color(0f, 1f, 0f, 1f)
                : new Color(1f, 0f, 0f, 1f);

            for (int i = 0; i < _scratch.Count; i++)
            {
                DrawCellWireRect(_scratch[i]);

                // Extra clarity: X marks on blocked
                if (!report.Allowed)
                    DrawCellX(_scratch[i]);
            }

            // Anchor highlight
            Gizmos.color = new Color(1f, 1f, 1f, 1f);
            DrawCellWireRect(anchor);
        }

        private void DrawPlacedFootprints()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 1f); // teal

            foreach (var kv in host.System.Instances)
            {
                var inst = kv.Value;
                var cells = inst.FootprintWorldCells;

                for (int i = 0; i < cells.Count; i++)
                    DrawCellWireRect(cells[i]);
            }
        }

        private void DrawCellWireRect(Cell2i cell)
        {
            var center = CellToWorldCenter(cell);
            var size = new Vector3(tileWorldSize, tileWorldSize, 0.001f); // XY
            Gizmos.DrawWireCube(center, size);
        }

        private void DrawCellX(Cell2i cell)
        {
            var c = CellToWorldCenter(cell);
            float h = tileWorldSize * 0.5f;

            var a = new Vector3(c.x - h, c.y - h, c.z);
            var b = new Vector3(c.x + h, c.y + h, c.z);
            var d = new Vector3(c.x - h, c.y + h, c.z);
            var e = new Vector3(c.x + h, c.y - h, c.z);

            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(d, e);
        }

        private Vector3 CellToWorldCenter(Cell2i cell)
        {
            float x = worldOrigin.x + (cell.X + 0.5f) * tileWorldSize;
            float y = worldOrigin.y + (cell.Y + 0.5f) * tileWorldSize;
            float z = worldOrigin.z + overlayZOffset; // push toward camera (tune as needed)
            return new Vector3(x, y, z);
        }
    }
}
