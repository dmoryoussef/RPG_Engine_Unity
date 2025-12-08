using UnityEngine;

namespace UI
{
    /// <summary>
    /// Minimal HUD contributor that wraps a single component and exposes
    /// its ToString() output as a HUD line, plus a simple click behavior.
    /// Also supports optional per-row panel color overrides.
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

        [Header("Optional Panel Styling")]
        [SerializeField]
        private bool _overridePanelColor = false;

        [SerializeField]
        private Color _panelColor = Color.white;

        public int Priority => _priority;
        public bool InMainPanelList => _inMainPanelList;
        public bool IsClickable => _isClickable;
        public HudClickMode ClickMode => _clickMode;

        // Exposed for HudRowWidget to read
        public bool OverridePanelColor => _overridePanelColor;
        public Color PanelColor => _panelColor;

        public string GetDisplayString()
        {
            if (_targetComponent == null)
                return "(null)";

            return _targetComponent.ToString();
        }

        public GameObject GetClickTarget()
        {
            if (_targetComponent != null)
                return _targetComponent.gameObject;

            return gameObject;
        }

        public void OnClick()
        {
            if (!_isClickable)
                return;

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
            var target = GetClickTarget() ?? gameObject;
            if (target == null)
                return;

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
