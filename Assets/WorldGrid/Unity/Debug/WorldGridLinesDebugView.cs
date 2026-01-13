using UnityEngine;
using WorldGrid.Runtime.Coords;
using WorldGrid.Unity;
using WorldGrid.Unity.Input;
using WorldGrid.Unity.Rendering;
using UnityEngine.LightTransport;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WorldGrid.Unity.Debug
{
    public sealed class WorldGridLinesDebugView : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private WorldHost worldHost;
        [SerializeField] private ChunkWorldRenderer rendererSource;
        [SerializeField] private WorldPointer2D pointer;

        [Header("Chunk Grid")]
        [SerializeField] private bool drawChunkBounds = true;
        [SerializeField] private bool drawChunkLabels = true;

        [Header("Tile Grid Around Mouse")]
        [SerializeField] private bool drawTileGridNearMouse = true;

        [Tooltip("Radius in cells around hovered cell. 0 = just the hovered cell bounds.")]
        [SerializeField] private int tileGridRadiusCells = 6;

        [Header("Style")]
        [SerializeField] private Color chunkGridColor = Color.green;
        [SerializeField] private Color tileGridColor = Color.green;

        [Tooltip("Z used for gizmo lines (world space).")]
        [SerializeField] private float gizmoZ = -0.05f;

        [Header("Hovered Chunk Emphasis")]
        [SerializeField] private bool drawHoveredChunkEmphasis = true;

        [Tooltip("If true, draw hovered chunk outline even when outside the renderer view window.")]
        [SerializeField] private bool drawHoveredChunkEvenIfOutsideView = true;

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

            if (drawChunkBounds || drawChunkLabels)
                DrawChunkGrid(root, world.ChunkSize, cellSize);

            if (drawTileGridNearMouse && pointer != null && pointer.CurrentHit.Valid)
                DrawTileGridNearMouse(root, cellSize, pointer.CurrentHit.Cell);
        }

        private void DrawChunkGrid(Transform root, int chunkSize, float cellSize)
        {
            Color prevG = Gizmos.color;
            Color prevH = Handles.color;

            Gizmos.color = chunkGridColor;
            Handles.color = chunkGridColor;

            ChunkCoord min = rendererSource.ViewChunkMin;
            Vector2Int size = rendererSource.ViewChunksSize;

            int w = Mathf.Max(1, size.x);
            int h = Mathf.Max(1, size.y);

            float chunkWorldSize = chunkSize * cellSize;

            for (int dy = 0; dy < h; dy++)
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

                    if (drawChunkBounds)
                    {
                        Gizmos.DrawLine(bl, br);
                        Gizmos.DrawLine(br, tr);
                        Gizmos.DrawLine(tr, tl);
                        Gizmos.DrawLine(tl, bl);
                    }

                    if (drawChunkLabels)
                    {
                        Vector3 center = (bl + tr) * 0.5f;
                        Handles.Label(center, $"Chunk {cx},{cy}");
                    }
                }

            Gizmos.color = prevG;
            Handles.color = prevH;
        }

        private static bool IsInView(ChunkCoord min, Vector2Int size, ChunkCoord cc)
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
#if UNITY_EDITOR
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
#endif
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

            // Vertical lines
            for (int x = minX; x <= maxX; x++)
            {
                float lx = x * cellSize;
                float ly0 = minY * cellSize;
                float ly1 = maxY * cellSize;

                Vector3 a = root.TransformPoint(new Vector3(lx, ly0, 0f)); a.z = gizmoZ;
                Vector3 b = root.TransformPoint(new Vector3(lx, ly1, 0f)); b.z = gizmoZ;
                Gizmos.DrawLine(a, b);
            }

            // Horizontal lines
            for (int y = minY; y <= maxY; y++)
            {
                float ly = y * cellSize;
                float lx0 = minX * cellSize;
                float lx1 = maxX * cellSize;

                Vector3 a = root.TransformPoint(new Vector3(lx0, ly, 0f)); a.z = gizmoZ;
                Vector3 b = root.TransformPoint(new Vector3(lx1, ly, 0f)); b.z = gizmoZ;
                Gizmos.DrawLine(a, b);
            }

            if (drawHoveredChunkEmphasis && pointer != null && pointer.CurrentHit.Valid)
            {
                ChunkCoord chunkHovered = pointer.CurrentHit.Chunk;

                bool isInView = IsInView(rendererSource.ViewChunkMin, rendererSource.ViewChunksSize, chunkHovered);

                if (drawHoveredChunkEvenIfOutsideView || isInView)
                    DrawHoveredChunkOutline(worldHost.WorldRoot, worldHost.ChunkSize, cellSize, chunkHovered);
            }


            Gizmos.color = prev;
        }
#endif
    }
}
