using System.Collections.Generic;
// WorldChunkStorageTransitionDebugLogger.cs
using UnityEngine;
using WorldGrid.Runtime.Chunks;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.World;
using WorldGrid.Unity;

namespace WorldGrid.Unity.Debug
{
    /// <summary>
    /// Debug-only logger for chunk lifecycle and storage transitions.
    /// Subscribes to SparseChunkWorld events:
    /// - ChunkCreated
    /// - ChunkRemoved
    /// - ChunkStorageKindChanged (Uniform ↔ Dense)
    ///
    /// Subscription is done in Start() to ensure WorldHost.World is initialized.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldChunkStorageTransitionDebugLogger : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private WorldHost worldHost;

        [Header("Logging")]
        [SerializeField] private bool logChunkCreated = true;
        [SerializeField] private bool logChunkRemoved = true;
        [SerializeField] private bool logKindTransitions = true;

        private SparseChunkWorld World => worldHost != null ? worldHost.World : null;

        private void Awake()
        {
            if (worldHost == null)
            {
                UnityEngine.Debug.LogError("WorldChunkStorageTransitionDebugLogger: worldHost not assigned.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            var w = World;
            if (w == null)
            {
                UnityEngine.Debug.LogError("WorldChunkStorageTransitionDebugLogger: worldHost.World is null at Start().", this);
                enabled = false;
                return;
            }

            w.ChunkCreated += OnChunkCreated;
            w.ChunkRemoved += OnChunkRemoved;
            w.ChunkStorageKindChanged += OnChunkStorageKindChanged;
        }

        private void OnDisable()
        {
            var w = World;
            if (w == null)
                return;

            w.ChunkCreated -= OnChunkCreated;
            w.ChunkRemoved -= OnChunkRemoved;
            w.ChunkStorageKindChanged -= OnChunkStorageKindChanged;
        }

        private void OnChunkCreated(ChunkCoord cc)
        {
            if (!logChunkCreated)
                return;

            UnityEngine.Debug.Log($"[ChunkCreated] {cc}", this);
        }

        private void OnChunkRemoved(ChunkCoord cc)
        {
            if (!logChunkRemoved)
                return;

            UnityEngine.Debug.Log($"[ChunkRemoved] {cc}", this);
        }

        private void OnChunkStorageKindChanged(ChunkCoord cc, ChunkStorageKind prev, ChunkStorageKind next)
        {
            if (!logKindTransitions)
                return;

            UnityEngine.Debug.Log($"[ChunkKind] {cc} {prev}->{next}", this);
        }
    }
}
