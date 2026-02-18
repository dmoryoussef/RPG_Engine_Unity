using UnityEngine;
using WorldPlacement.Runtime.Systems;
using WorldPlacement.Unity.Adapters;

namespace WorldPlacement.Unity.Host
{
    [DisallowMultipleComponent]
    public sealed class WorldPlacementHost : MonoBehaviour
    {
        [SerializeField] private WorldQueryAdapterBehaviour worldQuery;
        [SerializeField] private int defaultTileId = 0;

        [Header("(Debug only)")]
        [SerializeField] private int _totalBuildings = 0;
        public WorldPlacementSystem System { get; private set; }

        private void Awake()
        {
            if (worldQuery == null)
            {
                Debug.LogError("WorldPlacementHost: worldQuery adapter not assigned.", this);
                enabled = false;
                return;
            }

            System = new WorldPlacementSystem(worldQuery, defaultTileId);
        }

        private void Update()
        {
            _totalBuildings = System.Instances.Count;
        }
    }
}
