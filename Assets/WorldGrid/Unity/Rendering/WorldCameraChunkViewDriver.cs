using UnityEngine;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.Math;
using WorldGrid.Unity;
using WorldGrid.Unity.Rendering;

namespace WorldGrid.Unity.Debug
{
    /// <summary>
    /// Computes the chunk view window from the camera and drives ChunkWorldRenderer.
    /// Intended as the “production” replacement for manual fixed view windows.
    /// Keep manual nudger as an optional debug override.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldCameraChunkViewDriver : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private WorldHost worldHost;
        [SerializeField] private ChunkWorldRenderer rendererSource;

        [Header("Padding")]
        [Tooltip("Extra chunks beyond the camera bounds to prebuild (reduces pop-in).")]
        [SerializeField] private int marginChunks = 1;

        [Header("Update")]
        [Tooltip("If true, updates view every LateUpdate. If false, only updates when camera changes.")]
        [SerializeField] private bool alwaysUpdate = true;

        // Cache
        private Vector3 _lastCamPos;
        private float _lastOrthoSize;
        private float _lastAspect;

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera == null || worldHost == null || rendererSource == null)
            {
                UnityEngine.Debug.LogError("WorldCameraChunkViewDriver: Missing refs.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            SnapshotCamera();
            UpdateViewWindow();
        }

        private void LateUpdate()
        {
            if (!enabled)
                return;

            if (alwaysUpdate)
            {
                UpdateViewWindow();
                return;
            }

            if (CameraChanged())
            {
                SnapshotCamera();
                UpdateViewWindow();
            }
        }

        private bool CameraChanged()
        {
            if (targetCamera == null)
                return false;

            if (targetCamera.transform.position != _lastCamPos)
                return true;

            if (targetCamera.orthographic && Mathf.Abs(targetCamera.orthographicSize - _lastOrthoSize) > 0.0001f)
                return true;

            if (Mathf.Abs(targetCamera.aspect - _lastAspect) > 0.0001f)
                return true;

            return false;
        }

        private void SnapshotCamera()
        {
            _lastCamPos = targetCamera.transform.position;
            _lastAspect = targetCamera.aspect;
            _lastOrthoSize = targetCamera.orthographic ? targetCamera.orthographicSize : 0f;
        }

        private void UpdateViewWindow()
        {
            if (targetCamera == null || worldHost == null || rendererSource == null)
                return;

            if (!targetCamera.orthographic)
            {
                // For your current 2D tilemap setup, assume orthographic.
                // (If you later want perspective, we can implement plane intersection + bounds.)
                return;
            }

            // Camera world rect
            float halfH = targetCamera.orthographicSize;
            float halfW = halfH * targetCamera.aspect;

            Vector3 camPos = targetCamera.transform.position;

            // Convert camera bounds into WorldRoot-local space (world is drawn under WorldRoot).
            Transform root = worldHost.WorldRoot;
            if (root == null)
                return;

            Vector3 minW = new Vector3(camPos.x - halfW, camPos.y - halfH, 0f);
            Vector3 maxW = new Vector3(camPos.x + halfW, camPos.y + halfH, 0f);

            Vector3 minL = root.InverseTransformPoint(minW);
            Vector3 maxL = root.InverseTransformPoint(maxW);

            // Convert local space into cell coords.
            float cellSize = worldHost.CellSize;
            if (cellSize <= 0f)
                return;

            int minCellX = Mathf.FloorToInt(minL.x / cellSize);
            int minCellY = Mathf.FloorToInt(minL.y / cellSize);
            int maxCellX = Mathf.FloorToInt(maxL.x / cellSize);
            int maxCellY = Mathf.FloorToInt(maxL.y / cellSize);

            // Convert cell bounds -> chunk bounds (floor division handles negatives correctly).
            int chunkSize = worldHost.World != null ? worldHost.World.ChunkSize : 0;
            if (chunkSize <= 0)
                return;

            int minChunkX = MathUtil.FloorDiv(minCellX, chunkSize);
            int minChunkY = MathUtil.FloorDiv(minCellY, chunkSize);
            int maxChunkX = MathUtil.FloorDiv(maxCellX, chunkSize);
            int maxChunkY = MathUtil.FloorDiv(maxCellY, chunkSize);

            int pad = Mathf.Max(0, marginChunks);
            minChunkX -= pad; minChunkY -= pad;
            maxChunkX += pad; maxChunkY += pad;

            var viewMin = new ChunkCoord(minChunkX, minChunkY);
            var size = new Vector2Int(
                Mathf.Max(1, (maxChunkX - minChunkX + 1)),
                Mathf.Max(1, (maxChunkY - minChunkY + 1)));

            rendererSource.SetViewWindow(viewMin, size, pruneViewsOutsideWindow: true);
        }
    }
}
