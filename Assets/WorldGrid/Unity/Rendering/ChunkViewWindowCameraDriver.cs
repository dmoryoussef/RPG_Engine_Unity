using UnityEngine;
using WorldGrid.Runtime.Coords;

namespace WorldGrid.Unity.Rendering
{
    /// <summary>
    /// Computes chunk view window from the target camera's visible world bounds (zoom-aware),
    /// and writes it into a ChunkViewWindow. Safe to place on any GameObject (e.g. WorldHost).
    /// </summary>
    public sealed class ChunkViewWindowCameraDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private ChunkViewWindow viewWindow;

        [Header("World/Chunk Settings")]
        [Tooltip("World-space size of a chunk edge (chunkSizeInCells * cellSize).")]
        [SerializeField] private float chunkWorldSize = 16f;

        [Header("View Window Settings")]
        [Tooltip("Extra chunks added around the computed view window.")]
        [SerializeField] private int paddingChunks = 1;

        [Tooltip("Minimum view size in chunks.")]
        [SerializeField] private Vector2Int minViewSizeChunks = new Vector2Int(3, 3);

        [Tooltip("Maximum view size in chunks. Set <= 0 to disable.")]
        [SerializeField] private Vector2Int maxViewSizeChunks = new Vector2Int(0, 0);

        [Header("Perspective Support")]
        [Tooltip("Enable if your camera is perspective (zoom via FOV/distance).")]
        [SerializeField] private bool supportPerspective = true;

        [Tooltip("World Z plane used to estimate visible bounds for perspective cameras.")]
        [SerializeField] private float groundPlaneZ = 0f;

        [Header("Debug")]
        [SerializeField] private bool debugLog = false;

        private ChunkCoord _lastViewMin;
        private Vector2Int _lastViewSize;
        private bool _hasLast;

        private void Reset()
        {
            targetCamera = Camera.main;
        }

        private void OnEnable()
        {
            Camera.onPreCull += HandlePreCull;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= HandlePreCull;
        }

        private void HandlePreCull(Camera cam)
        {
            if (targetCamera == null || viewWindow == null)
                return;

            if (cam != targetCamera)
                return;

            UpdateViewFromCamera("PreCull");
        }

        private void LateUpdate()
        {
            // Fallback: if nothing renders this frame (Game view hidden), still keep updating.
            if (targetCamera == null || viewWindow == null)
            {
                if (debugLog)
                    UnityEngine.Debug.LogWarning("[ChunkViewWindowCameraDriver] Missing refs (camera/window).", this);
                return;
            }

            UpdateViewFromCamera("LateUpdate");
        }

        private void UpdateViewFromCamera(string source)
        {
            if (chunkWorldSize <= 0.0001f)
            {
                if (debugLog)
                    UnityEngine.Debug.LogWarning("[ViewDriver] chunkWorldSize invalid.", this);
                return;
            }

            if (!TryComputeVisibleWorldRect(out Rect worldRect))
            {
                if (debugLog)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[ViewDriver] Could not compute visible rect. " +
                        $"Ortho={targetCamera.orthographic} supportPerspective={supportPerspective}",
                        this);
                }
                return;
            }

            // Convert visible world bounds -> chunk bounds (exclusive max) with padding.
            // Floor on min, Ceil on max produces stable coverage and avoids drift.
            int minX = Mathf.FloorToInt(worldRect.xMin / chunkWorldSize) - paddingChunks;
            int minY = Mathf.FloorToInt(worldRect.yMin / chunkWorldSize) - paddingChunks;

            int maxX = Mathf.CeilToInt(worldRect.xMax / chunkWorldSize) + paddingChunks;
            int maxY = Mathf.CeilToInt(worldRect.yMax / chunkWorldSize) + paddingChunks;

            int desiredSizeX = Mathf.Max(0, maxX - minX);
            int desiredSizeY = Mathf.Max(0, maxY - minY);

            // Apply minimums.
            desiredSizeX = Mathf.Max(minViewSizeChunks.x, desiredSizeX);
            desiredSizeY = Mathf.Max(minViewSizeChunks.y, desiredSizeY);

            // Apply maximums (if set). If we clamp, keep the window centered on the desired range.
            if (maxViewSizeChunks.x > 0 && desiredSizeX > maxViewSizeChunks.x)
            {
                int centerXTimes2 = minX + maxX; // 2*center, avoids floats
                desiredSizeX = maxViewSizeChunks.x;
                minX = Mathf.FloorToInt((centerXTimes2 / 2f) - (desiredSizeX / 2f));
            }

            if (maxViewSizeChunks.y > 0 && desiredSizeY > maxViewSizeChunks.y)
            {
                int centerYTimes2 = minY + maxY;
                desiredSizeY = maxViewSizeChunks.y;
                minY = Mathf.FloorToInt((centerYTimes2 / 2f) - (desiredSizeY / 2f));
            }

            Vector2Int viewSize = new Vector2Int(desiredSizeX, desiredSizeY);
            ChunkCoord viewMin = new ChunkCoord(minX, minY);

            bool unchanged = _hasLast && viewMin.Equals(_lastViewMin) && viewSize == _lastViewSize;
            if (unchanged)
                return;

            _hasLast = true;
            _lastViewMin = viewMin;
            _lastViewSize = viewSize;

            if (debugLog)
            {
                UnityEngine.Debug.Log(
                    $"[ViewDriver:{source}] SetView min({viewMin.X},{viewMin.Y}) size({viewSize.x},{viewSize.y}) " +
                    $"rect(xMin={worldRect.xMin:0.00},yMin={worldRect.yMin:0.00},xMax={worldRect.xMax:0.00},yMax={worldRect.yMax:0.00}) " +
                    $"camOrtho={targetCamera.orthographic} orthoSize={targetCamera.orthographicSize:0.00}",
                    this);
            }

            viewWindow.SetView(viewMin, viewSize);
        }

        private bool TryComputeVisibleWorldRect(out Rect worldRect)
        {
            if (targetCamera.orthographic)
            {
                float height = targetCamera.orthographicSize * 2f;
                float width = height * targetCamera.aspect;

                Vector3 pos = targetCamera.transform.position;
                worldRect = new Rect(pos.x - width * 0.5f, pos.y - height * 0.5f, width, height);
                return true;
            }

            if (!supportPerspective)
            {
                worldRect = default;
                return false;
            }

            // Estimate visible quad on plane Z = groundPlaneZ by ray-plane intersects at viewport corners.
            Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, groundPlaneZ));

            if (!TryRayPlane(targetCamera, new Vector2(0f, 0f), plane, out Vector3 p00) ||
                !TryRayPlane(targetCamera, new Vector2(1f, 0f), plane, out Vector3 p10) ||
                !TryRayPlane(targetCamera, new Vector2(0f, 1f), plane, out Vector3 p01) ||
                !TryRayPlane(targetCamera, new Vector2(1f, 1f), plane, out Vector3 p11))
            {
                worldRect = default;
                return false;
            }

            float minX = Mathf.Min(p00.x, p10.x, p01.x, p11.x);
            float maxX = Mathf.Max(p00.x, p10.x, p01.x, p11.x);
            float minY = Mathf.Min(p00.y, p10.y, p01.y, p11.y);
            float maxY = Mathf.Max(p00.y, p10.y, p01.y, p11.y);

            worldRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private static bool TryRayPlane(Camera cam, Vector2 viewport01, Plane plane, out Vector3 hitPoint)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(viewport01.x, viewport01.y, 0f));
            if (plane.Raycast(ray, out float t))
            {
                hitPoint = ray.GetPoint(t);
                return true;
            }

            hitPoint = default;
            return false;
        }

        private void OnValidate()
        {
            chunkWorldSize = Mathf.Max(0.01f, chunkWorldSize);
            paddingChunks = Mathf.Max(0, paddingChunks);

            minViewSizeChunks.x = Mathf.Max(1, minViewSizeChunks.x);
            minViewSizeChunks.y = Mathf.Max(1, minViewSizeChunks.y);
        }
    }
}
