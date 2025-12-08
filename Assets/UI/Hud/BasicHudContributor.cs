using UnityEngine;

namespace UI
{
    /// <summary>
    /// Minimal HUD contributor that wraps a single Component and exposes
    /// its ToString() output as a HUD line, plus a simple click behavior.
    /// Also supports an optional static panel color override.
    /// </summary>
    public class BasicHudContributor : MonoBehaviour, IHudContributor
    {
        [Header("Target")]
        [SerializeField]
        private Component _targetComponent;

        [Header("HUD")]
        [SerializeField]
        private int _priority = 0;

        [SerializeField]
        private bool _inMainPanelList = true;

        [SerializeField]
        private bool _isClickable = true;

        [SerializeField]
        private HudClickMode _clickMode = HudClickMode.InspectOwner;

        [Header("Panel Color")]
        [SerializeField]
        private bool _overridePanelColor = false;

        [SerializeField]
        private Color _panelColor = Color.white;

        [Header("Debug")]
        [SerializeField]
        protected bool _debugLogging = false;

        public int Priority
        {
            get { return _priority; }
        }

        public bool InMainPanelList
        {
            get { return _inMainPanelList; }
        }

        public bool IsClickable
        {
            get { return _isClickable; }
        }

        public HudClickMode ClickMode
        {
            get { return _clickMode; }
        }

        /// <summary>
        /// True if this contributor is configured to override the panel color.
        /// </summary>
        public bool OverridePanelColor
        {
            get { return _overridePanelColor; }
        }

        /// <summary>
        /// The static panel color configured in the inspector.
        /// </summary>
        public Color PanelColor
        {
            get { return _panelColor; }
        }

        public virtual string GetDisplayString()
        {
            if (_targetComponent == null)
            {
                return "(null)";
            }

            return _targetComponent.ToString();
        }

        public virtual Color GetColor()
        {
            if (_overridePanelColor)
            {
                return _panelColor;
            }

            // Default: no tint (white). You can make this transparent or theme-specific later.
            return Color.white;
        }

        public virtual GameObject GetClickTarget()
        {
            if (_targetComponent != null)
            {
                return _targetComponent.gameObject;
            }

            return gameObject;
        }

        public virtual void OnClick()
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

        protected void HandleInspectOwner()
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

            // TODO: plug into existing inspection pipeline here.
            if (_debugLogging)
            {
                Debug.Log($"[HUD] InspectOwner not yet wired for {name}", this);
            }
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
