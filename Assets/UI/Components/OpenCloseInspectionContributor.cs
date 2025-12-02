using Interaction;
using UnityEngine;

namespace UI
{
    [DisallowMultipleComponent]
    public sealed class OpenCloseInspectionContributor : MonoBehaviour, IInspectionPanelContributor
    {
        [Header("State Source")]
        [SerializeField] private OpenCloseState _openCloseState;

        // Root is resolved lazily from the context, not in Awake.
        private InspectionPanelRoot _panelRoot;

        private void Awake()
        {
            if (_openCloseState == null)
            {
                _openCloseState =
                    GetComponent<OpenCloseState>() ??
                    GetComponentInParent<OpenCloseState>(includeInactive: true);
            }
        }

        private void OnEnable()
        {
            if (_openCloseState != null)
                _openCloseState.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_openCloseState != null)
                _openCloseState.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(bool oldIsOpen, bool newIsOpen)
        {
            // If we've never been inspected, _panelRoot will still be null.
            // That’s fine: no inspection window is open = nothing to refresh.
            if (_panelRoot == null || _openCloseState == null)
                return;

            _panelRoot.NotifySourceStateChanged(_openCloseState.gameObject);
        }

        public void ContributeToPanel(InspectionPanelContext ctx)
        {
            if (ctx == null || _openCloseState == null)
                return;

            // Grab the specific panel root from the context the first time.
            if (_panelRoot == null && ctx.PanelRoot != null)
            {
                _panelRoot = ctx.PanelRoot;
            }

            bool isOpen = _openCloseState.IsOpen;
            //bool isLocked = _openCloseState.IsLocked;

            // --- State line ---
            //if (isLocked)
            //{
            //    ctx.AddState("Locked", priority: 10, category: "Door");
            //}
            //else
            {
                ctx.AddState(isOpen ? "Open" : "Closed", priority: 10, category: "Door");
            }

            // --- Action button ---
            //ctx.AddAction(
            //    label: isOpen ? "Close" : "Open",
            //    callback: () =>
            //    {
            //        _openCloseState.TryStateChange(OpenCloseAction.Toggle);
            //        // OnStateChanged will fire; HandleStateChanged will relay
            //        // to the correct panel root if one is inspecting this object.
            //    },
            //    //isEnabled: !isLocked,
            //    //disabledReason: isLocked ? "Locked" : null,
            //    priority: 10,
            //    category: "Door"
            //);
        }
    }
}
