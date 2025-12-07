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
            if (_userHudRootPrefab == null)
            {
                Debug.LogWarning("[HUD] UserHudRoot prefab not assigned.", this);
                return;
            }

            if (_instance != null)
            {
                return;
            }
            var canvas = GameObject.FindFirstObjectByType<Canvas>();
            Transform parent = canvas != null ? canvas.transform : null;

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
