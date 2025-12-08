using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Root controller for the in-game user HUD.
    ///
    /// Responsibilities:
    /// - Receive the currently controlled entity root.
    /// - Discover IHudContributors under that entity (preferring "HudContributors" child).
    /// - Build one HudRowWidget per contributor in the appropriate container.
    /// - Ask each row to refresh every frame.
    ///
    /// It does NOT know about concrete systems (Targeter, Health, Inventory, etc.).
    /// Contributors own their own click behavior and update frequency (internally).
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

        private GameObject _controlledRoot;
        private readonly List<HudRowWidget> _rows = new List<HudRowWidget>();

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
            RefreshRows();
        }

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

        private static List<IHudContributor> FindContributors(GameObject root)
        {
            var result = new List<IHudContributor>();

            if (root == null)
            {
                return result;
            }

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
            {
                return;
            }

            foreach (var contributor in group)
            {
                if (contributor == null)
                {
                    continue;
                }

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
