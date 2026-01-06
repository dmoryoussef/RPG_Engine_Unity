using TMPro;
using UnityEngine;
using Targeting;
using Inspection;

namespace UI
{
    /// <summary>
    /// Hover prompt driven by the TargetingContextModel (Hover channel).
    /// - Subscribes to TargeterBase.Model.HoverChanged
    /// - Reads FocusTarget info (label, anchor, distance)
    /// - Pulls prompt strings from InspectableComponent (on anchor or parent chain)
    /// </summary>
    public sealed class HoverPromptUI : MonoBehaviour
    {
        [Header("Targeting Source")]
        [SerializeField] private TargeterBase _targeter; // PlayerTargeter derives from this

        [Header("UI Refs")]
        [SerializeField] private RectTransform _root;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _promptText;

        [Header("Mouse Follow")]
        [SerializeField] private Vector2 _screenOffset = new Vector2(16f, -16f);
        [SerializeField] private bool _clampToScreen = true;
        [SerializeField] private float _screenPadding = 8f;

        private FocusTarget _currentHover;
        private InspectableComponent _currentInspectable;

        private void Awake()
        {
            if (_root != null)
                _root.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (_targeter != null && _targeter.Model != null)
                _targeter.Model.HoverChanged += OnHoverChanged;
        }

        private void OnDisable()
        {
            if (_targeter != null && _targeter.Model != null)
                _targeter.Model.HoverChanged -= OnHoverChanged;
        }

        private void Update()
        {
            if (_root == null) return;

            if (_root.gameObject.activeSelf)
                UpdateMouseFollow();
        }

        private void OnHoverChanged(FocusChange change)
        {
            _currentHover = change.Current;

            if (_currentHover == null)
            {
                _currentInspectable = null;
                Hide();
                return;
            }

            // Prefer inspectable from the specific anchor object we hovered.
            var anchorGo = _currentHover.Anchor != null ? _currentHover.Anchor.gameObject : null;
            _currentInspectable = anchorGo != null
                ? anchorGo.GetComponentInParent<InspectableComponent>()
                : _currentHover.LogicalTarget?.TargetTransform != null
                    ? _currentHover.LogicalTarget.TargetTransform.GetComponentInParent<InspectableComponent>()
                    : null;

            // If there's no InspectableComponent, we treat it as "no prompt UI"
            if (_currentInspectable == null)
            {
                Hide();
                return;
            }

            Show(_currentHover, _currentInspectable);
        }

        private void Show(FocusTarget hover, InspectableComponent inspectable)
        {
            if (!_root.gameObject.activeSelf)
                _root.gameObject.SetActive(true);

            if (_nameText != null)
            {
                // Prefer inspectable DisplayName, fallback to targeting label.
                string name = !string.IsNullOrWhiteSpace(inspectable.DisplayName)
                    ? inspectable.DisplayName
                    : (hover.TargetLabel ?? "(unnamed)");

                _nameText.text = name;
            }

            if (_promptText != null)
            {
                // For MVP: hover prompt is always "available" while hovered.
                // If you later want gate reasons (too far / face target), feed that in from your interaction gating layer.
                string hint = inspectable.HoverInputHint;
                string action = inspectable.HoverAction;

                if (string.IsNullOrWhiteSpace(action))
                    action = "Inspect";

                _promptText.text = string.IsNullOrWhiteSpace(hint) ? action : $"{hint} {action}";
            }
        }

        private void Hide()
        {
            if (_root != null && _root.gameObject.activeSelf)
                _root.gameObject.SetActive(false);
        }

        private void UpdateMouseFollow()
        {
            Vector2 p = (Vector2)Input.mousePosition + _screenOffset;

            if (_clampToScreen)
            {
                Vector2 size = _root.rect.size;

                float minX = _screenPadding;
                float minY = _screenPadding;
                float maxX = Screen.width - _screenPadding - size.x;
                float maxY = Screen.height - _screenPadding - size.y;

                p.x = Mathf.Clamp(p.x, minX, maxX);
                p.y = Mathf.Clamp(p.y, minY, maxY);
            }

            _root.position = p;
        }
    }
}
