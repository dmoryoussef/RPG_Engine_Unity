using UnityEngine;

namespace UI
{
    /// <summary>
    /// Spawns the UserHudRoot prefab and wires it to the currently controlled entity.
    /// </summary>
    public class UserHudSpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField]
        private UserHudRoot _userHudRootPrefab;

        [Header("References")]
        [SerializeField]
        private GameObject _controlledEntityRoot;
        [SerializeField, Tooltip("Optional canvas to parent the HUD to. If null, will search for any Canvas in the scene.")]
        private Canvas _parentCanvas;

        private UserHudRoot _instance;

        private void Awake()
        {
            if (_controlledEntityRoot == null)
            {
                _controlledEntityRoot = gameObject;
            }
        }

        private void Start()
        {
            SpawnHud();
        }

        private void SpawnHud()
        {
            if (_parentCanvas == null)
            {
                Debug.LogWarning("[HUD] No Canvas found in the scene to parent the HUD to.", this);
            }
            if (_userHudRootPrefab == null)
            {
                Debug.LogWarning("[HUD] UserHudRoot prefab not assigned.", this);
                return;
            }

            if (_instance != null)
            {
                return;
            }
            Transform parent = _parentCanvas != null ? _parentCanvas.transform : null;

            _instance = Instantiate(_userHudRootPrefab, parent);
            _instance.Initialize(_controlledEntityRoot);
        }

        public void SetControlledEntity(GameObject newEntityRoot)
        {
            _controlledEntityRoot = newEntityRoot;

            if (_instance != null)
            {
                _instance.Initialize(_controlledEntityRoot);
            }
        }
    }
}
