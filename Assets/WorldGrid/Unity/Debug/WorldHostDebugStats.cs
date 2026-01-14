using UnityEngine;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.World;

namespace WorldGrid.Unity.Debug
{
    [DisallowMultipleComponent]
    public sealed class WorldHostDebugStats : MonoBehaviour
    {
        [SerializeField] private WorldHost worldHost;

        [Header("Log")]
        [SerializeField] private bool logChunkCreated = true;
        [SerializeField] private bool logChunkRemoved = true;

        [Header("Read-Only")]
        [SerializeField] private int totalChunks;

        private SparseChunkWorld World => worldHost != null ? worldHost.World : null;

        private void Awake()
        {
            if (worldHost == null)
            {
                UnityEngine.Debug.LogError("WorldHostDebugStats: worldHost not assigned.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            var w = World;
            if (w == null)
            {
                UnityEngine.Debug.LogError("WorldHostDebugStats: worldHost.World is null.", this);
                enabled = false;
                return;
            }

            w.ChunkCreated += OnChunkCreated;
            w.ChunkRemoved += OnChunkRemoved;
        }

        private void OnDisable()
        {
            var w = World;
            if (w == null)
                return;

            w.ChunkCreated -= OnChunkCreated;
            w.ChunkRemoved -= OnChunkRemoved;
        }

        private void Update()
        {
            var w = World;
            totalChunks = w != null ? w.ChunkCount : 0;
        }

        private void OnChunkCreated(ChunkCoord cc)
        {
            if (logChunkCreated)
                UnityEngine.Debug.Log($"[ChunkCreated] {cc}", this);
        }

        private void OnChunkRemoved(ChunkCoord cc)
        {
            if (logChunkRemoved)
                UnityEngine.Debug.Log($"[ChunkRemoved] {cc}", this);
        }
    }
}
