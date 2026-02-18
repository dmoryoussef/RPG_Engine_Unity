using UnityEngine;
using WorldGrid.Unity;
using WorldGrid.Unity.Input;
using WorldGrid.Unity.UI;

namespace WorldGrid.Unity.Tilemap
{
    /// <summary>
    /// Visual-only hover + brush footprint preview.
    /// - Highlights hovered cell
    /// - Draws NxN brush outline (based on TileBrushState.brushRadius)
    /// - Changes tint for paint vs erase while mouse button is held
    /// No world mutation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldBrushPreviewOverlay : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private WorldHost worldHost;
        [SerializeField] private WorldPointer2D pointer;
        [SerializeField] private TileBrushState brushState;

        [Header("Renderers")]
        [SerializeField] private SpriteRenderer hoverCellFill;
        [SerializeField] private LineRenderer brushOutline;

        [Header("Colors")]
        [SerializeField] private Color hoverNeutral = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color hoverPaint = new Color(0.2f, 1f, 0.2f, 0.25f);
        [SerializeField] private Color hoverErase = new Color(1f, 0.2f, 0.2f, 0.25f);
        [SerializeField] private Color hoverNoSelection = new Color(1f, 1f, 0.2f, 0.20f);

        [SerializeField] private Color outlineNeutral = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color outlinePaint = new Color(0.2f, 1f, 0.2f, 0.9f);
        [SerializeField] private Color outlineErase = new Color(1f, 0.2f, 0.2f, 0.9f);
        [SerializeField] private Color outlineNoSelection = new Color(1f, 1f, 0.2f, 0.8f);

        [Header("Z Offset (draw order)")]
        [SerializeField] private float zOffset = -0.05f;

        private enum Intent { None, Paint, Erase }

        private Intent _intent = Intent.None;
        private bool _hoverValid;

        private int _cellX;
        private int _cellY;

        private readonly Vector3[] _outlinePts = new Vector3[5];

        public void Bind(WorldHost host, WorldPointer2D worldPointer, TileBrushState state)
        {
            worldHost = host;
            pointer = worldPointer;
            brushState = state;
        }

        private void Awake()
        {
            // Safe defaults: hidden until we have a valid hover.
            setVisible(false);
        }

        private void OnEnable()
        {
            if (pointer != null)
            {
                pointer.HoverEntered += onHoverEntered;
                pointer.HoverExited += onHoverExited;
                pointer.HoverChanged += onHoverChanged;

                pointer.PointerDown += onPointerDown;
                pointer.PointerUp += onPointerUp;
            }

            if (brushState != null)
            {
                // Selection changes can affect "no selection" coloring.
                brushState.OnSelectionChanged += onSelectionChanged;
            }

            // If we enable while already hovering, force refresh from current hit.
            if (pointer != null && pointer.CurrentHit.Valid)
                applyHover(pointer.CurrentHit);
        }

        private void OnDisable()
        {
            if (pointer != null)
            {
                pointer.HoverEntered -= onHoverEntered;
                pointer.HoverExited -= onHoverExited;
                pointer.HoverChanged -= onHoverChanged;

                pointer.PointerDown -= onPointerDown;
                pointer.PointerUp -= onPointerUp;
            }

            if (brushState != null)
                brushState.OnSelectionChanged -= onSelectionChanged;

            setVisible(false);
        }

        private void onSelectionChanged(int _)
        {
            // Only colors change; geometry is unchanged.
            if (_hoverValid)
                applyColors();
        }

        private void onPointerDown(WorldPointerHit hit, int button)
        {
            if (button == 0) _intent = Intent.Paint;
            else if (button == 1) _intent = Intent.Erase;

            if (_hoverValid)
                applyColors();
        }

        private void onPointerUp(WorldPointerHit hit, int button)
        {
            // When both are up, return to neutral.
            if (button == 0 && _intent == Intent.Paint) _intent = Intent.None;
            if (button == 1 && _intent == Intent.Erase) _intent = Intent.None;

            if (_hoverValid)
                applyColors();
        }

        private void onHoverEntered(WorldPointerHit hit) => applyHover(hit);
        private void onHoverChanged(WorldPointerHit prev, WorldPointerHit next) => applyHover(next);

        private void onHoverExited(WorldPointerHit hit)
        {
            _hoverValid = false;
            setVisible(false);
          
        }

        private void applyHover(WorldPointerHit hit)
        {
            if (worldHost == null || pointer == null || brushState == null)
            {
                setVisible(false);
                return;
            }

            if (!hit.Valid)
            {
                _hoverValid = false;
                setVisible(false);
                return;
            }

            _hoverValid = true;

            _cellX = hit.Cell.X;
            _cellY = hit.Cell.Y;

            setVisible(true);
            applyGeometry();
            applyColors();
        }

        private void applyGeometry()
        {
            float cellSize = worldHost.CellSize;
            if (cellSize <= 0f)
                return;

            Transform root = worldHost.WorldRoot;
            if (root == null)
                return;

            int r = Mathf.Max(0, brushState.brushRadius);
            int n = (r * 2) + 1;

            // Hover cell center in local space
            Vector3 cellCenterLocal = new Vector3(
                (_cellX + 0.5f) * cellSize,
                (_cellY + 0.5f) * cellSize,
                0f
            );

            Vector3 cellCenterWorld = root.TransformPoint(cellCenterLocal);
            cellCenterWorld.z += zOffset;

            // Hover fill scales to exactly 1 cell
            if (hoverCellFill != null)
            {
                hoverCellFill.transform.position = cellCenterWorld;
                hoverCellFill.transform.rotation = root.rotation;
                hoverCellFill.transform.localScale = new Vector3(cellSize, cellSize, 1f);
            }

            // Brush outline rectangle for NxN cells, centered on hovered cell
            if (brushOutline != null)
            {
                float size = n * cellSize;

                // Bottom-left corner in local space
                float blX = (_cellX - r) * cellSize;
                float blY = (_cellY - r) * cellSize;

                Vector3 bl = root.TransformPoint(new Vector3(blX, blY, 0f));
                Vector3 br = root.TransformPoint(new Vector3(blX + size, blY, 0f));
                Vector3 tr = root.TransformPoint(new Vector3(blX + size, blY + size, 0f));
                Vector3 tl = root.TransformPoint(new Vector3(blX, blY + size, 0f));

                bl.z += zOffset;
                br.z += zOffset;
                tr.z += zOffset;
                tl.z += zOffset;

                _outlinePts[0] = bl;
                _outlinePts[1] = br;
                _outlinePts[2] = tr;
                _outlinePts[3] = tl;
                _outlinePts[4] = bl;

                brushOutline.positionCount = 5;
                brushOutline.SetPositions(_outlinePts);
            }
        }

        private void applyColors()
        {
            bool hasSelection = brushState.selectedTileId >= 0;

            Color fill;
            Color outline;

            if (!hasSelection && _intent == Intent.Paint)
            {
                // Paint intent with no selection: warn-y color.
                fill = hoverNoSelection;
                outline = outlineNoSelection;
            }
            else
            {
                switch (_intent)
                {
                    case Intent.Paint:
                        fill = hoverPaint;
                        outline = outlinePaint;
                        break;

                    case Intent.Erase:
                        fill = hoverErase;
                        outline = outlineErase;
                        break;

                    default:
                        fill = hoverNeutral;
                        outline = outlineNeutral;
                        break;
                }
            }

            if (hoverCellFill != null)
                hoverCellFill.color = fill;

            if (brushOutline != null)
            {
                // Avoid per-frame gradient allocations by setting colors directly.
                brushOutline.startColor = outline;
                brushOutline.endColor = outline;
            }
        }

        private void setVisible(bool visible)
        {
            if (hoverCellFill != null)
                hoverCellFill.enabled = visible;

            if (brushOutline != null)
                brushOutline.enabled = visible;
        }
    }
}
