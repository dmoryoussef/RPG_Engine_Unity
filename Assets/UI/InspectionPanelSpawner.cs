using UnityEngine;
using Inspection;

namespace UI
{
    /// <summary>
    /// Listens to an InspectorComponent and spawns / destroys an inspection panel prefab.
    /// Keeps the core InspectorComponent free of any UI references.
    /// </summary>
    public sealed class InspectionPanelSpawner : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private InspectorComponent _inspector;

        [Header("UI Prefab")]
        [Tooltip("Prefab that has an InspectionPanelRoot component at its root.")]
        [SerializeField] private GameObject _panelPrefab;

        [Header("Parent")]
        [Tooltip("Optional parent transform to spawn under (e.g. a Canvas). If null, spawns at root.")]
        [SerializeField] private Transform _panelParent;

        private InspectionPanelRoot _activePanel;
        private GameObject _activeInstance;

        private void Awake()
        {
            if (_inspector == null)
            {
                _inspector = FindFirstObjectByType<InspectorComponent>();
                if (_inspector == null)
                {
                    Debug.LogWarning($"{name}: No InspectorComponent assigned or found in scene.", this);
                }
            }

            if (_panelPrefab == null)
            {
                Debug.LogWarning($"{name}: No panel prefab assigned.", this);
            }
        }

        private void OnEnable()
        {
            if (_inspector != null)
            {
                _inspector.InspectionCompleted += HandleInspectionCompleted;
                _inspector.InspectionCleared += HandleInspectionCleared;
            }
        }

        private void OnDisable()
        {
            if (_inspector != null)
            {
                _inspector.InspectionCompleted -= HandleInspectionCompleted;
                _inspector.InspectionCleared -= HandleInspectionCleared;
            }
        }

        private void HandleInspectionCompleted(InspectionData data)
        {
            if (_panelPrefab == null)
                return;

            // Create instance if needed.
            if (_activePanel == null)
            {
                _activeInstance = Instantiate(_panelPrefab, _panelParent);
                _activePanel = _activeInstance.GetComponent<InspectionPanelRoot>();

                if (_activePanel == null)
                {
                    Debug.LogError(
                        $"{name}: Spawned panel prefab does not have an InspectionPanelRoot component.",
                        _activeInstance);
                    return;
                }
            }

            _activePanel.Show(data);
        }

        private void HandleInspectionCleared()
        {
            if (_activePanel != null)
            {
                _activePanel.Hide();
            }

            // If you want to fully destroy it instead of just hiding:
            // if (_activeInstance != null)
            // {
            //     Destroy(_activeInstance);
            //     _activeInstance = null;
            //     _activePanel = null;
            // }
        }
    }
}
