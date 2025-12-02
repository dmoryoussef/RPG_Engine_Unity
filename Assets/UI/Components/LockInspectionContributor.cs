using Interaction;
using UnityEngine;

namespace UI
{
    [DisallowMultipleComponent]
    public sealed class LockInspectionContributor : MonoBehaviour, IInspectionPanelContributor
    {
        [Header("State Source")]
        [SerializeField] private LockState _lockState;

        // Root is resolved lazily from the context, not in Awake.
        private InspectionPanelRoot _panelRoot;

        private void Awake()
        {
            if (_lockState == null)
            {
                _lockState =
                    GetComponent<LockState>() ??
                    GetComponentInParent<LockState>(includeInactive: true);
            }
        }

        private void OnEnable()
        {
            if (_lockState != null)
                _lockState.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_lockState != null)
                _lockState.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(bool oldIsOpen, bool newIsOpen)
        {
            // If we've never been inspected, _panelRoot will still be null.
            // That’s fine: no inspection window is open = nothing to refresh.
            if (_panelRoot == null || _lockState == null)
                return;

            _panelRoot.NotifySourceStateChanged(_lockState.gameObject);
        }

        public void ContributeToPanel(InspectionPanelContext ctx)
        {
            if (ctx == null || _lockState == null)
                return;

            // Grab the specific panel root from the context the first time.
            if (_panelRoot == null && ctx.PanelRoot != null)
            {
                _panelRoot = ctx.PanelRoot;
            }

            bool isLocked = _lockState.IsLocked;
            {
                ctx.AddState(isLocked ? "Locked" : "Unlocked", priority: 10, category: "Door");
            }

            // --- Action button ---
            ctx.AddAction(
                label: isLocked ? "Unlock" : "Lock",
                callback: () =>
                {
                    _lockState.TryStateChange(LockAction.Toggle);
                    // OnStateChanged will fire; HandleStateChanged will relay
                    // to the correct panel root if one is inspecting this object.
                },
                //isEnabled: !isLocked,
                //disabledReason: isLocked ? "Locked" : null,
                priority: 10,
                category: "Door"
            );
        }
    }
}
