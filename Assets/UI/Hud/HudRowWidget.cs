using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class HudRowWidget : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Button _button;
        [SerializeField] private LayoutElement _layoutElement;
        [SerializeField] private float _verticalPadding = 4f;

        private Func<string> _getLabel;
        private Action _onClicked;
        private bool _isClickable;

        public void InitializeSingle(IHudContributor contributor)
        {
            _getLabel = contributor.GetDisplayString;
            _onClicked = contributor.OnClick;
            _isClickable = contributor.IsClickable;

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(HandleClick);
            }

            Refresh();
        }

        public void Refresh()
        {
            if (_label != null && _getLabel != null)
                _label.text = _getLabel();

            if (_button != null)
                _button.interactable = _isClickable;

            AutoSizeToLabel();
        }

        private void AutoSizeToLabel()
        {
            if (_label == null || _layoutElement == null)
                return;

            // Make sure TMP has an up-to-date layout
            _label.ForceMeshUpdate();

            // Ask TMP how tall it wants to be for this width
            float preferredHeight = _label.preferredHeight;

            _layoutElement.preferredHeight = preferredHeight + _verticalPadding;
            _layoutElement.minHeight = preferredHeight + _verticalPadding;

            // Let the layout system know something changed
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        private void HandleClick()
        {
            if (!_isClickable) return;
            _onClicked?.Invoke();
        }
    }
}
