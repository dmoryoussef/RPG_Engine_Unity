using UnityEngine;
using State;

namespace UI
{
    public sealed class PanelContributorComponent : MonoBehaviour, IInspectionPanelContributor
    {
        [Header("State Source")]
        [Tooltip("Component deriving from State.BaseState. " +
                 "If not set, will auto-find on this GameObject.")]
        [SerializeField] private BaseState state;

        private InspectionPanelRoot _panelRoot;

        private void Awake()
        {
            if (!state)
                state = GetComponent<BaseState>();
        }

        private void OnEnable()
        {
            if (state != null)
                state.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (state != null)
                state.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(BaseState _)
        {
            if (_panelRoot == null) return;
            _panelRoot.NotifySourceStateChanged(gameObject);
        }

        public void ContributeToPanel(InspectionPanelContext ctx)
        {
            if (ctx == null || state == null)
                return;

            if (_panelRoot == null && ctx.PanelRoot != null)
                _panelRoot = ctx.PanelRoot;

            ctx.AddState(
                label: state.GetDescriptionText(),
                priority: state.GetDescriptionPriority(),
                category: state.GetDescriptionCategory()
            );
        }
    }
}
