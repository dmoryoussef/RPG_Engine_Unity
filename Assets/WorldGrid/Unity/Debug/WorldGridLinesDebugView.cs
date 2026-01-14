using UnityEngine;
using UnityEngine.Serialization;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.Math;
using WorldGrid.Unity.Input;
using WorldGrid.Unity.Rendering;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WorldGrid.Unity.Debug
{
    /// <summary>
    /// Debug grid visualization for:
    /// - Renderer view window (ViewChunkMin/ViewChunksSize)
    /// - Camera-derived view window (optional)
    /// - Existing chunks inside camera window (optional)
    /// - Tile grid near mouse (cell grid)
    /// - Hovered chunk emphasis
    ///
    /// Label drawing is gated by a master toggle + per-source toggles.
    /// </summary>
    public sealed class WorldGridLinesDebugView : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private WorldHost worldHost;
        [SerializeField] private ChunkWorldRenderer rendererSource;
        [SerializeField] private WorldPointer2D pointer;

        [Header("Renderer View Chunk Grid")]
        [FormerlySerializedAs("drawChunkBounds")]
        [SerializeField] private bool drawRendererChunkBounds = true;

        [FormerlySerializedAs("drawChunkLabels")]
        [SerializeField] private bool drawRendererChunkLabels = true;

        [Header("Camera View Chunk Grid")]
        [SerializeField] private bool drawCameraChunkBounds = false;
        [SerializeField] private bool drawCameraChunkLabels = false;

        [Tooltip("Camera used for camera-derived bounds. If null, uses Camera.main.")]
        [SerializeField] private Camera cameraSource;

        [Tooltip("Extra chunks padding around camera bounds.")]
        [SerializeField] private int cameraMarginChunks = 1;

        [Header("Existing Chunks In Camera View")]
        [SerializeField] private bool drawExistingChunksInCameraView = false;
        [SerializeField] private bool labelExistingChunks = false;

        [Header("Tile Grid Around Mouse")]
        [SerializeField] private bool drawTileGridNearMouse = true;

        [Tooltip("Radius in cells around hovered cell. 0 = just the hovered cell bounds.")]
        [SerializeField] private int tileGridRadiusCells = 6;

        [Header("Labels")]
        [Tooltip("Master label toggle for this component (gates all Handles.Label calls).")]
        [SerializeField] private bool enableLabels = true;

        [Header("Label Position")]
        [SerializeField] private Vector3 labelPositionOffset = Vector3.zero;

        [Header("Label Offsets")]
        [SerializeField] private float labelWorldYOffset = 0.15f;
        [SerializeField] private float labelPixelYOffset = 14f;
        [SerializeField] private bool labelOffsetInScreenSpace = true;


        [Tooltip("Gate renderer-view chunk labels (R Chunk x,y).")]
        [SerializeField] private bool enableRendererLabels = true;

        [Tooltip("Gate camera-view chunk labels (C Chunk x,y).")]
        [SerializeField] private bool enableCameraLabels = true;

        [Tooltip("Gate existing-chunk labels (E x,y).")]
        [SerializeField] private bool enableExistingChunkLabels = true;

        [Header("Style")]
        [SerializeField] private Color rendererChunkGridColor = Color.green;
        [SerializeField] private Color cameraChunkGridColor = new Color(0.2f, 0.8f, 1.0f, 1f);
        [SerializeField] private Color existingChunksColor = new Color(1.0f, 0.6f, 0.2f, 1f);
        [SerializeField] private Color tileGridColor = Color.green;

        [Tooltip("Z used for gizmo lines (world space).")]
        [SerializeField] private float gizmoZ = -0.05f;

        [Header("Hovered Chunk Emphasis")]
        [SerializeField] private bool drawHoveredChunkEmphasis = true;

        [Tooltip("If true, draw hovered chunk outline even when outside the renderer view window.")]
        [FormerlySerializedAs("drawHoveredChunkEvenIfOutsideView")]
        [SerializeField] private bool drawHoveredChunkEvenIfOutsideRendererView = true;

        [SerializeField] private Color hoveredChunkColor = Color.yellow;

        [Tooltip("Thickness in pixels for hovered chunk outline (editor Handles).")]
        [SerializeField] private float hoveredChunkThickness = 6f;

        [SerializeField] private float hoveredChunkGizmoZ = -0.04f;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!enabled)
                return;

            if (worldHost == null || rendererSource == null)
                return;

            var world = worldHost.World;
            if (world == null)
                return;

            float cellSize = worldHost.CellSize;
            if (cellSize <= 0f)
                return;

            Transform root = worldHost.WorldRoot;
            if (root == null)
                return;

            // 1) Renderer view grid
            if (drawRendererChunkBounds || drawRendererChunkLabels)
            {
                DrawChunkGrid(
                    root: root,
                    chunkSize: world.ChunkSize,
                    cellSize: cellSize,
                    min: rendererSource.ViewChunkMin,
                    size: rendererSource.ViewChunksSize,
                    color: rendererChunkGridColor,
                    drawBounds: drawRendererChunkBounds,
                    drawLabels: enableLabels && enableRendererLabels && drawRendererChunkLabels,
                    labelPrefix: "R",
                    labelStackIndex: 0);
            }

            // 2) Camera-derived grid + existing chunks overlay
            if (drawCameraChunkBounds || drawCameraChunkLabels || drawExistingChunksInCameraView)
            {
                if (TryGetCameraChunkWindow(
                        worldChunkSize: world.ChunkSize,
                        cellSize: cellSize,
                        root: root,
                        out ChunkCoord camMin,
                        out Vector2Int camSize))
                {
                    if (drawCameraChunkBounds || drawCameraChunkLabels)
                    {
                        DrawChunkGrid(
                            root: root,
                            chunkSize: world.ChunkSize,
                            cellSize: cellSize,
                            min: camMin,
                            size: camSize,
                            color: cameraChunkGridColor,
                            drawBounds: drawCameraChunkBounds,
                            drawLabels: enableLabels && enableCameraLabels && drawCameraChunkLabels,
                            labelPrefix: "C",
                            labelStackIndex: 1);
                    }

                    if (drawExistingChunksInCameraView)
                    {
                        DrawExistingChunksInWindow(
                            root: root,
                            world: world,
                            cellSize: cellSize,
                            windowMin: camMin,
                            windowSize: camSize,
                            drawLabels: enableLabels && enableExistingChunkLabels && labelExistingChunks);
                    }
                }
            }

            // 3) Tile grid around mouse
            if (drawTileGridNearMouse && pointer != null && pointer.CurrentHit.Valid)
                DrawTileGridNearMouse(root, cellSize, pointer.CurrentHit.Cell);

            // 4) Hovered chunk emphasis
            if (drawHoveredChunkEmphasis && pointer != null && pointer.CurrentHit.Valid)
            {
                ChunkCoord hoveredChunk = pointer.CurrentHit.Chunk;

                bool inRendererView = IsInWindow(
                    rendererSource.ViewChunkMin,
                    rendererSource.ViewChunksSize,
                    hoveredChunk);

                if (drawHoveredChunkEvenIfOutsideRendererView || inRendererView)
                    DrawHoveredChunkOutline(root, world.ChunkSize, cellSize, hoveredChunk);
            }
        }

        private void DrawChunkLabel(Vector3 chunkTopLeftWorld, float chunkWorldSize, int cx, int cy, string labelPrefix, int labelStackIndex)
        {
            Vector3 worldOffset = new Vector3(
                labelPositionOffset.x * chunkWorldSize,
                labelPositionOffset.y * chunkWorldSize,
                0f);

            Vector3 worldAnchor = chunkTopLeftWorld + worldOffset;

            string text = $"{labelPrefix} {cx},{cy}";

            if (!labelOffsetInScreenSpace)
            {
                worldAnchor.y += labelWorldYOffset * labelStackIndex;
                Handles.Label(worldAnchor, text);
                return;
            }

            // Screen-space (pixel) stacking so zoom doesn't change spacing
            Vector2 gui = HandleUtility.WorldToGUIPoint(worldAnchor);
            gui.y += labelPixelYOffset * labelStackIndex;

            Handles.BeginGUI();
            GUI.Label(new Rect(gui.x, gui.y, 120f, 18f), text);
            Handles.EndGUI();
        }



        private void DrawChunkGrid(
            Transform root,
            int chunkSize,
            float cellSize,
            ChunkCoord min,
            Vector2Int size,
            Color color,
            bool drawBounds,
            bool drawLabels,
            string labelPrefix,
            int labelStackIndex)
        {
            Color prevG = Gizmos.color;
            Color prevH = Handles.color;

            Gizmos.color = color;
            Handles.color = color;

            int w = Mathf.Max(1, size.x);
            int h = Mathf.Max(1, size.y);

            float chunkWorldSize = chunkSize * cellSize;

            for (int dy = 0; dy < h; dy++)
            {
                for (int dx = 0; dx < w; dx++)
                {
                    int cx = min.X + dx;
                    int cy = min.Y + dy;

                    float x0 = cx * chunkWorldSize;
                    float y0 = cy * chunkWorldSize;

                    Vector3 bl = root.TransformPoint(new Vector3(x0, y0, 0f)); bl.z = gizmoZ;
                    Vector3 br = root.TransformPoint(new Vector3(x0 + chunkWorldSize, y0, 0f)); br.z = gizmoZ;
                    Vector3 tr = root.TransformPoint(new Vector3(x0 + chunkWorldSize, y0 + chunkWorldSize, 0f)); tr.z = gizmoZ;
                    Vector3 tl = root.TransformPoint(new Vector3(x0, y0 + chunkWorldSize, 0f)); tl.z = gizmoZ;

                    if (drawBounds)
                    {
                        Gizmos.DrawLine(bl, br);
                        Gizmos.DrawLine(br, tr);
                        Gizmos.DrawLine(tr, tl);
                        Gizmos.DrawLine(tl, bl);
                    }

                    if (drawLabels)
                    {
                        Vector3 topLeft = new Vector3(bl.x, tr.y, gizmoZ);
                        DrawChunkLabel(topLeft, chunkWorldSize, cx, cy, labelPrefix, labelStackIndex);
                    }
                }
            }

            Gizmos.color = prevG;
            Handles.color = prevH;
        }

        private static bool IsInWindow(ChunkCoord min, Vector2Int size, ChunkCoord cc)
        {
            int w = Mathf.Max(1, size.x);
            int h = Mathf.Max(1, size.y);

            return cc.X >= min.X
                   && cc.X < min.X + w
                   && cc.Y >= min.Y
                   && cc.Y < min.Y + h;
        }

        private void DrawHoveredChunkOutline(Transform root, int chunkSize, float cellSize, ChunkCoord cc)
        {
            Color prevH = Handles.color;
            Handles.color = hoveredChunkColor;

            float chunkWorldSize = chunkSize * cellSize;

            float x0 = cc.X * chunkWorldSize;
            float y0 = cc.Y * chunkWorldSize;

            Vector3 bl = root.TransformPoint(new Vector3(x0, y0, 0f)); bl.z = hoveredChunkGizmoZ;
            Vector3 br = root.TransformPoint(new Vector3(x0 + chunkWorldSize, y0, 0f)); br.z = hoveredChunkGizmoZ;
            Vector3 tr = root.TransformPoint(new Vector3(x0 + chunkWorldSize, y0 + chunkWorldSize, 0f)); tr.z = hoveredChunkGizmoZ;
            Vector3 tl = root.TransformPoint(new Vector3(x0, y0 + chunkWorldSize, 0f)); tl.z = hoveredChunkGizmoZ;

            Handles.DrawAAPolyLine(
                Mathf.Max(1f, hoveredChunkThickness),
                new Vector3[] { bl, br, tr, tl, bl });

            Handles.color = prevH;
        }

        private void DrawTileGridNearMouse(Transform root, float cellSize, CellCoord hovered)
        {
            Color prev = Gizmos.color;
            Gizmos.color = tileGridColor;

            int r = Mathf.Max(0, tileGridRadiusCells);

            int minX = hovered.X - r;
            int maxX = hovered.X + r + 1;
            int minY = hovered.Y - r;
            int maxY = hovered.Y + r + 1;

            for (int x = minX; x <= maxX; x++)
            {
                float lx = x * cellSize;
                float ly0 = minY * cellSize;
                float ly1 = maxY * cellSize;

                Vector3 a = root.TransformPoint(new Vector3(lx, ly0, 0f)); a.z = gizmoZ;
                Vector3 b = root.TransformPoint(new Vector3(lx, ly1, 0f)); b.z = gizmoZ;
                Gizmos.DrawLine(a, b);
            }

            for (int y = minY; y <= maxY; y++)
            {
                float ly = y * cellSize;
                float lx0 = minX * cellSize;
                float lx1 = maxX * cellSize;

                Vector3 a = root.TransformPoint(new Vector3(lx0, ly, 0f)); a.z = gizmoZ;
                Vector3 b = root.TransformPoint(new Vector3(lx1, ly, 0f)); b.z = gizmoZ;
                Gizmos.DrawLine(a, b);
            }

            Gizmos.color = prev;
        }

        private bool TryGetCameraChunkWindow(
            int worldChunkSize,
            float cellSize,
            Transform root,
            out ChunkCoord minChunk,
            out Vector2Int sizeChunks)
        {
            minChunk = default;
            sizeChunks = default;

            Camera cam = cameraSource != null ? cameraSource : Camera.main;
            if (cam == null)
                return false;

            if (!cam.orthographic)
                return false;

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;

            Vector3 camPos = cam.transform.position;

            Vector3 minW = new Vector3(camPos.x - halfW, camPos.y - halfH, 0f);
            Vector3 maxW = new Vector3(camPos.x + halfW, camPos.y + halfH, 0f);

            Vector3 minL = root.InverseTransformPoint(minW);
            Vector3 maxL = root.InverseTransformPoint(maxW);

            int minCellX = Mathf.FloorToInt(minL.x / cellSize);
            int minCellY = Mathf.FloorToInt(minL.y / cellSize);
            int maxCellX = Mathf.FloorToInt(maxL.x / cellSize);
            int maxCellY = Mathf.FloorToInt(maxL.y / cellSize);

            int minChunkX = MathUtil.FloorDiv(minCellX, worldChunkSize);
            int minChunkY = MathUtil.FloorDiv(minCellY, worldChunkSize);
            int maxChunkX = MathUtil.FloorDiv(maxCellX, worldChunkSize);
            int maxChunkY = MathUtil.FloorDiv(maxCellY, worldChunkSize);

            int pad = Mathf.Max(0, cameraMarginChunks);
            minChunkX -= pad;
            minChunkY -= pad;
            maxChunkX += pad;
            maxChunkY += pad;

            minChunk = new ChunkCoord(minChunkX, minChunkY);

            sizeChunks = new Vector2Int(
                Mathf.Max(1, (maxChunkX - minChunkX + 1)),
                Mathf.Max(1, (maxChunkY - minChunkY + 1)));

            return true;
        }

        private void DrawExistingChunksInWindow(
            Transform root,
            WorldGrid.Runtime.World.SparseChunkWorld world,
            float cellSize,
            ChunkCoord windowMin,
            Vector2Int windowSize,
            bool drawLabels)
        {
            Color prevG = Gizmos.color;
            Color prevH = Handles.color;

            Gizmos.color = existingChunksColor;
            Handles.color = existingChunksColor;

            float chunkWorldSize = world.ChunkSize * cellSize;

            foreach (var kv in world.Chunks)
            {
                ChunkCoord cc = kv.Key;

                if (!IsInWindow(windowMin, windowSize, cc))
                    continue;

                float x0 = cc.X * chunkWorldSize;
                float y0 = cc.Y * chunkWorldSize;

                Vector3 bl = root.TransformPoint(new Vector3(x0, y0, 0f)); bl.z = gizmoZ;
                Vector3 br = root.TransformPoint(new Vector3(x0 + chunkWorldSize, y0, 0f)); br.z = gizmoZ;
                Vector3 tr = root.TransformPoint(new Vector3(x0 + chunkWorldSize, y0 + chunkWorldSize, 0f)); tr.z = gizmoZ;
                Vector3 tl = root.TransformPoint(new Vector3(x0, y0 + chunkWorldSize, 0f)); tl.z = gizmoZ;

                Gizmos.DrawLine(bl, br);
                Gizmos.DrawLine(br, tr);
                Gizmos.DrawLine(tr, tl);
                Gizmos.DrawLine(tl, bl);

                if (drawLabels)
                {
                    Vector3 topLeft = new Vector3(bl.x, tr.y, gizmoZ);
                    DrawChunkLabel(topLeft, chunkWorldSize, cc.X, cc.Y, "E", labelStackIndex: 2);
                }
            }

            Gizmos.color = prevG;
            Handles.color = prevH;
        }
#endif
    }
}