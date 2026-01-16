using UnityEngine;
using WorldGrid.Runtime.Tiles;
using WorldGrid.Unity.Assets;

namespace WorldGrid.Unity.Debug
{
    public sealed class TileLibraryDebugger : MonoBehaviour
    {
        [SerializeField] private TileLibraryAsset tileLibraryAsset;
        [SerializeField] private int tileIdToInspect = 0;

        private TileLibrary _runtime;

        private void Awake()
        {
            if (tileLibraryAsset == null)
            {
                UnityEngine.Debug.LogWarning("TileLibraryDebugger: No TileLibraryAsset assigned.");
                return;
            }

            _runtime = tileLibraryAsset.BuildRuntime();
        }

        [ContextMenu("Log Tile Info")]
        public void LogTileInfo()
        {
            if (_runtime == null)
            {
                UnityEngine.Debug.LogWarning("TileLibraryDebugger: Runtime library not built (missing asset?)");
                return;
            }

            UnityEngine.Debug.Log(_runtime.ToDebugString(tileIdToInspect));
        }
    }
}
