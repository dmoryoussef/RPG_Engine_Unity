using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Root controller for the in-game HUD.
    /// 
    /// Responsibilities:
    /// - Receive the currently controlled entity root.
    /// - Discover IHudContributors under that entity (preferring "HudContributors" child).
    /// - Build one HudRowWidget per contributor in the appropriate container.
    /// - Periodically refresh rows by calling widget.Refresh().
    /// 
    /// It does NOT know about Targeter, Inspector, or any other systems.
    /// Contributors own their click behavior via IHudContributor.OnClick().
    /// </summary>
    public class UserHudRoot : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField]
        private Transform _mainPanelContainer;

        [SerializeField]
        private Transform _outsidePanelContainer;

        [Header("Prefabs")]
        [SerializeField]
        private HudRowWidget _rowPrefab;

        [Header("Refresh Settings")]
        [SerializeField]
        private float _refreshInterval = 0.2f;

        private GameObject _controlledRoot;

        private readonly List<HudRowWidget> _rows = new List<HudRowWidget>();
        private float _refreshTimer;

        /// <summary>
        /// Called by the spawner / owner to wire the currently controlled entity.
        /// </summary>
        public void Initialize(GameObject controlledRoot)
        {
            _controlledRoot = controlledRoot;
            Rebuild();
        }

        private void OnDisable()
        {
            ClearRows();
        }

        private void Update()
        {
            _refreshTimer += Time.deltaTime;

            if (_refreshTimer >= _refreshInterval)
            {
                _refreshTimer = 0f;
                RefreshRows();
            }
        }

        /// <summary>
        /// Rebuild all HUD rows from the current controlled root.
        /// Safe to call when switching control to a new entity.
        /// </summary>
        private void Rebuild()
        {
            ClearRows();

            if (_controlledRoot == null)
            {
                return;
            }

            var contributors = FindContributors(_controlledRoot);

            var main = contributors
                .Where(c => c.InMainPanelList)
                .OrderByDescending(c => c.Priority)
                .ToList();

            var outside = contributors
                .Where(c => !c.InMainPanelList)
                .OrderByDescending(c => c.Priority)
                .ToList();

            BuildRowsForGroup(main, _mainPanelContainer);
            BuildRowsForGroup(outside, _outsidePanelContainer);
        }

        /// <summary>
        /// Finds all IHudContributor instances under the entity.
        /// Prefers a "HudContributors" child if present, otherwise scans the full hierarchy.
        /// </summary>
        private static List<IHudContributor> FindContributors(GameObject root)
        {
            var result = new List<IHudContributor>();

            if (root == null)
                return result;

            var hudContributorsRoot = root.transform.Find("HudContributors");
            if (hudContributorsRoot != null)
            {
                result.AddRange(hudContributorsRoot.GetComponentsInChildren<IHudContributor>(true));
            }
            else
            {
                result.AddRange(root.GetComponentsInChildren<IHudContributor>(true));
            }

            return result;
        }

        private void BuildRowsForGroup(IEnumerable<IHudContributor> group, Transform parent)
        {
            if (parent == null || _rowPrefab == null)
                return;

            foreach (var contributor in group)
            {
                if (contributor == null)
                    continue;

                var widget = Instantiate(_rowPrefab, parent);
                widget.InitializeSingle(contributor);
                _rows.Add(widget);
            }
        }

        private void ClearRows()
        {
            foreach (var widget in _rows)
            {
                if (widget != null)
                {
                    Destroy(widget.gameObject);
                }
            }

            _rows.Clear();
        }

        private void RefreshRows()
        {
            foreach (var widget in _rows)
            {
                if (widget != null)
                {
                    widget.Refresh();
                }
            }
        }
    }
}
