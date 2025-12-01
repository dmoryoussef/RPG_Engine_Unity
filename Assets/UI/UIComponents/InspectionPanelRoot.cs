using System.Collections.Generic;
using UnityEngine;
using Inspection;

namespace UI
{
    /// <summary>
    /// Root inspection panel controller.
    /// Owns a collection of subpanels, builds a context via contributors,
    /// and controls visibility.
    /// </summary>
    public sealed class InspectionPanelRoot : MonoBehaviour
    {
        [Header("Root")]
        [Tooltip("If null, this GameObject is treated as the root.")]
        [SerializeField] private GameObject _rootObject;

        [Header("Subpanels (Explicit)")]
        [Tooltip("Optional explicit list of subpanels (MonoBehaviours that implement IInspectionSubpanel).")]
        [SerializeField] private List<MonoBehaviour> _subpanelBehaviours = new();

        [Header("Discovery")]
        [Tooltip("If true, also auto-discovers any IInspectionSubpanel components under this root.")]
        [SerializeField] private bool _autoDiscoverChildSubpanels = true;

        private readonly List<IInspectionSubpanel> _subpanels = new();
        private InspectionData _currentData;
        private InspectionPanelContext _currentContext;
        private bool _isOpen;

        public InspectionPanelContext CurrentContext => _currentContext;
        public InspectionData CurrentData => _currentData;
        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_rootObject == null)
                _rootObject = gameObject;

            CacheExplicitSubpanels();

            if (_autoDiscoverChildSubpanels)
                AutoDiscoverSubpanels();

            SetRootActive(false);
            _isOpen = false;
        }

        // --------- State-change bridge (unchanged) ---------

        public void NotifySourceStateChanged(GameObject changedRoot)
        {
            if (!_isOpen || _currentData == null || changedRoot == null)
                return;

            var inspectedRoot = _currentData.TargetRoot;
            if (inspectedRoot == null)
                return;

            bool sameHierarchy =
                changedRoot == inspectedRoot ||
                changedRoot.transform.IsChildOf(inspectedRoot.transform) ||
                inspectedRoot.transform.IsChildOf(changedRoot.transform);

            if (!sameHierarchy)
                return;

            Refresh();
        }

        // --------- Subpanel registration ---------

        private void CacheExplicitSubpanels()
        {
            _subpanels.Clear();

            foreach (var mb in _subpanelBehaviours)
            {
                if (mb == null) continue;

                if (mb is IInspectionSubpanel panel && !_subpanels.Contains(panel))
                {
                    _subpanels.Add(panel);
                }
                else if (!(mb is IInspectionSubpanel))
                {
                    Debug.LogWarning(
                        $"{name}: Assigned MonoBehaviour {mb.name} does not implement IInspectionSubpanel.",
                        this);
                }
            }
        }

        private void AutoDiscoverSubpanels()
        {
            var discovered = GetComponentsInChildren<MonoBehaviour>(includeInactive: true);

            foreach (var mb in discovered)
            {
                if (mb == null) continue;

                if (mb is IInspectionSubpanel panel && !_subpanels.Contains(panel))
                {
                    _subpanels.Add(panel);
                }
            }
        }

        public void RegisterSubpanel(IInspectionSubpanel panel)
        {
            if (panel == null) return;
            if (_subpanels.Contains(panel)) return;

            _subpanels.Add(panel);

            if (_isOpen && _currentData != null)
            {
                panel.OnPopulate(_currentData, _currentContext);
                panel.OnOpen();
            }
        }

        public void UnregisterSubpanel(IInspectionSubpanel panel)
        {
            if (panel == null) return;

            if (_subpanels.Remove(panel) && _isOpen)
            {
                panel.OnClose();
            }
        }

        // --------- Open / Close / Refresh ---------

        private void SetRootActive(bool active)
        {
            if (_rootObject != null && _rootObject.activeSelf != active)
                _rootObject.SetActive(active);
        }

        public void Show(InspectionData data)
        {
            if (data == null)
            {
                Debug.LogWarning($"{name}: Show called with null InspectionData.", this);
                return;
            }

            _currentData = data;
            _currentContext = BuildContextFromContributors(data);

            foreach (var panel in _subpanels)
            {
                panel.OnPopulate(_currentData, _currentContext);
            }

            SetRootActive(true);
            _isOpen = true;

            foreach (var panel in _subpanels)
            {
                panel.OnOpen();
            }
        }

        public void Refresh()
        {
            if (!_isOpen || _currentData == null)
                return;

            _currentContext = BuildContextFromContributors(_currentData);

            foreach (var panel in _subpanels)
            {
                panel.OnPopulate(_currentData, _currentContext);
            }
        }

        public void Hide()
        {
            if (!_isOpen)
                return;

            foreach (var panel in _subpanels)
            {
                panel.OnClose();
            }

            SetRootActive(false);
            _isOpen = false;
            _currentData = null;
            _currentContext = null;
        }

        // --------- Context building ---------

        private InspectionPanelContext BuildContextFromContributors(InspectionData data)
        {
            var ctx = new InspectionPanelContext(data, this);

            if (data.TargetRoot != null)
            {
                var contributors =
                    data.TargetRoot.GetComponentsInChildren<IInspectionPanelContributor>(includeInactive: true);

                foreach (var contributor in contributors)
                {
                    if (contributor == null) continue;
                    contributor.ContributeToPanel(ctx);
                }
            }

            ctx.States.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            ctx.Actions.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            ctx.Sections.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            return ctx;
        }

    }
}
