using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Inspection;

namespace UI
{
    public sealed class InteractionActionsSubpanel : MonoBehaviour, IInspectionSubpanel
    {
        [Header("Wiring")]
        [Tooltip("Parent transform that will contain spawned action buttons.")]
        [SerializeField] private Transform _actionsContainer;

        [Tooltip("Button prefab with a TMP_Text child for the label.")]
        [SerializeField] private Button _actionButtonPrefab;

        private readonly List<Button> _spawnedButtons = new();

        public void OnPopulate(InspectionData data, InspectionPanelContext context)
        {
            ClearButtons();

            if (context == null || context.Actions.Count == 0)
                return;

            if (_actionsContainer == null || _actionButtonPrefab == null)
            {
                Debug.LogWarning($"{name}: Actions container or button prefab is not assigned.", this);
                return;
            }

            foreach (var entry in context.Actions)
            {
                var button = Instantiate(_actionButtonPrefab, _actionsContainer);
                _spawnedButtons.Add(button);

                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = entry.Label;

                button.interactable = entry.IsEnabled;

                var localEntry = entry;
                button.onClick.AddListener(() =>
                {
                    if (localEntry.Callback != null && localEntry.IsEnabled)
                    {
                        localEntry.Callback.Invoke();
                        // State components will raise events
                        // and the root will Refresh() when needed.
                    }
                });
            }
        }

        public void OnOpen()
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
        }

        public void OnClose()
        {
            ClearButtons();
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void ClearButtons()
        {
            foreach (var b in _spawnedButtons)
            {
                if (b != null)
                    Destroy(b.gameObject);
            }
            _spawnedButtons.Clear();
        }
    }
}
