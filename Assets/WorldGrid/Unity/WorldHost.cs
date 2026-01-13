using Core;
using UnityEngine;
using WorldGrid.Runtime.World;

namespace WorldGrid.Unity
{
    public sealed class WorldHost : MonoBehaviour
    {
        [Header("World Settings (Runtime)")]
        [SerializeField] private int chunkSize = 32;
        [SerializeField] private int defaultTileId = 0;

        [Header("World Mapping (Unity)")]
        [Tooltip("Unity units per cell. Renderer + pointer should both use this value.")]
        [SerializeField] private float cellSize = 1f;

        [Tooltip("Optional. If null, uses this transform. Used for centering / presentation offsets later.")]
        [SerializeField] private Transform worldRoot;

        public SparseChunkWorld World { get; private set; }

        public int ChunkSize => chunkSize;
        public int DefaultTileId => defaultTileId;

        public float CellSize => cellSize;

        public Transform WorldRoot => worldRoot != null ? worldRoot : transform;

        private void Awake()
        {
            Registry.Register<WorldHost>(this);

            World = new SparseChunkWorld(chunkSize, defaultTileId);
            UnityEngine.Debug.Log($"WorldHost created world with chunkSize={chunkSize}, defaultTileId={defaultTileId}", this);
        }

        private void OnDestroy()
        {
            Registry.Unregister<WorldHost>(this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (chunkSize < 1)
                chunkSize = 1;

            if (cellSize < 0.0001f)
                cellSize = 0.0001f;
        }
#endif
    }
}
