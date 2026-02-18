using Core;
using UnityEngine;
using WorldGrid.Runtime.World;

namespace WorldGrid.Unity
{
    /// <summary>
    /// Unity-side owner for a SparseChunkWorld instance.
    /// Responsible for world lifetime, configuration, and global access.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldHost : MonoBehaviour
    {
        #region Inspector - World Settings (Runtime)

        [Header("World Settings (Runtime)")]
        [SerializeField] private int chunkSize = 32;
        [SerializeField] private int defaultTileId = 0;

        #endregion

        #region Inspector - World Mapping (Unity)

        [Header("World Mapping (Unity)")]
        [Tooltip("Unity units per cell. Renderer and pointer must use the same value.")]
        [SerializeField] private float cellSize = 1f;

        [Tooltip("Optional. If null, this transform is used.")]
        [SerializeField] private Transform worldRoot;

        #endregion

        #region Properties

        public SparseChunkWorld World { get; private set; }

        public int ChunkSize => chunkSize;
        public int DefaultTileId => defaultTileId;
        public float CellSize => cellSize;

        public Transform WorldRoot => worldRoot != null ? worldRoot : transform;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            registerSelf();

            createWorld();

            logWorldCreated();
        }

        private void OnDestroy()
        {
            unregisterSelf();
        }

        #endregion

        #region World Creation

        private void createWorld()
        {
            World = new SparseChunkWorld(chunkSize, defaultTileId);
        }

        #endregion

        #region Registry

        private void registerSelf()
        {
            Registry.Register<WorldHost>(this);
        }

        private void unregisterSelf()
        {
            Registry.Unregister<WorldHost>(this);
        }

        #endregion

        #region Logging

        private void logWorldCreated()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log(
                $"WorldHost created world (chunkSize={chunkSize}, defaultTileId={defaultTileId}, cellSize={cellSize})",
                this);
#endif
        }

        #endregion

#if UNITY_EDITOR
        #region Editor Validation

        private void OnValidate()
        {
            clampInspectorValues();
        }

        private void clampInspectorValues()
        {
            if (chunkSize < 1)
                chunkSize = 1;

            if (cellSize < 0.0001f)
                cellSize = 0.0001f;
        }

        #endregion
#endif
    }
}
