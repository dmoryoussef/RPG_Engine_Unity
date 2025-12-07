using UnityEngine;

namespace UI
{
    /// <summary>
    /// Minimal HUD contributor that wraps a single component and exposes
    /// its ToString() output as a HUD line, plus a simple click behavior.
    /// </summary>
    public class BasicHudContributor : MonoBehaviour, IHudContributor
    {
        [SerializeField]
        private Component _targetComponent;

        [SerializeField]
        private int _priority = 0;

        [SerializeField]
        private bool _inMainPanelList = true;

        [SerializeField]
        private bool _isClickable = true;

        [SerializeField]
        private HudClickMode _clickMode = HudClickMode.InspectOwner;

        [SerializeField]
        private bool _debugLogging = false;

        public int Priority => _priority;
        public bool InMainPanelList => _inMainPanelList;
        public bool IsClickable => _isClickable;
        public HudClickMode ClickMode => _clickMode;

        public string GetDisplayString()
        {
            if (_targetComponent == null)
            {
                return "(null)";
            }

            return _targetComponent.ToString();
        }

        public GameObject GetClickTarget()
        {
            if (_targetComponent != null)
            {
                return _targetComponent.gameObject;
            }

            return gameObject;
        }

        public void OnClick()
        {
            if (!_isClickable)
            {
                return;
            }

            switch (_clickMode)
            {
                case HudClickMode.None:
                    return;

                case HudClickMode.InspectOwner:
                    HandleInspectOwner();
                    break;
            }
        }

        private void HandleInspectOwner()
        {
            var target = GetClickTarget();
            if (target == null)
            {
                target = gameObject;
            }

            if (target == null)
            {
                return;
            }

            // 🔗 This is the *only* place we touch the inspection pipeline for MVP.
            // Wire this up however your existing Inspector works.
            //
            // Examples (pick what matches your project):
            //
            // 1) Global singleton:
            // InspectorComponent.Instance.BeginInspection(target);
            //
            // 2) Find on scene root:
            // var inspector = Object.FindObjectOfType<InspectorComponent>();
            // if (inspector != null) inspector.BeginInspection(target);
            //
            // 3) Find up the hierarchy:
            // var inspector = target.GetComponentInParent<InspectorComponent>();
            // if (inspector != null) inspector.BeginInspection(target);
            //
            // For now I'll just leave a TODO to plug into your real inspector.

            // TODO: plug into existing inspection pipeline here.
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_debugLogging)
            {
                Debug.Log($"[HUD] {name} bound to component: {_targetComponent}", this);
            }
        }
#endif
    }
}
