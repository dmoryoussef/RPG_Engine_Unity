using Interaction;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Generic contributor that turns any InteractableBase into a simple
    /// "Interact" button in the inspection panel.
    /// Useful as a fallback when you don't have a more specific contributor.
    /// </summary>
    public sealed class InteractableInspectionContributor : MonoBehaviour, IInspectionPanelContributor
    {
        [SerializeField] private InteractableBase _interactable;

        private void Awake()
        {
            if (_interactable == null)
            {
                _interactable = GetComponent<InteractableBase>() ??
                                GetComponentInParent<InteractableBase>(true);
            }
        }

        public void ContributeToPanel(InspectionPanelContext context)
        {
            if (_interactable == null || context == null)
                return;

            // generic interact button
            context.AddAction(
                label: "Interact",
                callback: () => _interactable.OnInteract(),
                isEnabled: true,
                disabledReason: null,
                priority: 0
            );
        }
    }
}
