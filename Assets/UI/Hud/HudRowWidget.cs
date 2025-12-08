using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// HUD row widget that:
    /// - Takes a text block from an IHudContributor
    /// - Splits it into lines and creates one TMP label per line under a VerticalLayoutGroup
    /// - Interprets tiny line-level formatting:
    ///     "! " prefix  => warning text color + bold
    ///     "- " prefix  => bullet line ("• " + text)
    ///     "---" line   => divider / dim text
    /// - Polls contributor.GetColor() every refresh to tint the panel background.
    /// </summary>
    [RequireComponent(typeof(LayoutElement))]
    public class HudRowWidget : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField]
        private Transform _linesRoot;

        [SerializeField]
        private TextMeshProUGUI _linePrefab;

        [SerializeField]
        private Button _button;

        [SerializeField]
        private Image _background;

        [Header("Layout")]
        [SerializeField]
        private LayoutElement _layoutElement;

        [SerializeField]
        private float _verticalPadding = 4f;

        [Header("Text Colors")]
        [SerializeField]
        private Color _normalColor = Color.white;

        [SerializeField]
        private Color _warningColor = new Color(1f, 0.8f, 0.3f);

        [SerializeField]
        private Color _dividerColor = new Color(1f, 1f, 1f, 0.4f);

        [SerializeField]
        private Color _bulletColor = Color.white;

        private readonly List<TextMeshProUGUI> _lines = new List<TextMeshProUGUI>();

        private IHudContributor _contributor;
        private bool _initialized;

        private void Reset()
        {
            if (_layoutElement == null)
            {
                _layoutElement = GetComponent<LayoutElement>();
            }

            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            if (_background == null)
            {
                _background = GetComponent<Image>();
            }

            if (_background == null)
            {
                _background = GetComponentInChildren<Image>(true);
            }

            if (_linesRoot == null && transform.childCount > 0)
            {
                _linesRoot = transform.GetChild(0);
            }

            if (_linePrefab == null && _linesRoot != null)
            {
                _linePrefab = _linesRoot.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        private void Awake()
        {
            if (_layoutElement == null)
            {
                _layoutElement = GetComponent<LayoutElement>();
            }

            if (_background == null)
            {
                _background = GetComponent<Image>();
            }

            if (_background == null)
            {
                _background = GetComponentInChildren<Image>(true);
            }
        }

        private void OnEnable()
        {
            if (_initialized)
            {
                Refresh();
            }
        }

        /// <summary>
        /// Initialize this row for a single IHudContributor.
        /// Called once when the row is created.
        /// </summary>
        public void InitializeSingle(IHudContributor contributor)
        {
            _contributor = contributor;
            _initialized = true;

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(HandleClick);
                _button.interactable = _contributor.IsClickable;
            }

            // Initial refresh so the row is correct on first frame.
            Refresh();
        }

        /// <summary>
        /// Called by the HUD root every frame to update text, color, and layout.
        /// </summary>
        public void Refresh()
        {
            if (!_initialized || _contributor == null)
            {
                return;
            }

            // 1) Text
            string block = _contributor.GetDisplayString();
            UpdateLines(block);

            // 2) Background color
            ApplyPanelColor(_contributor.GetColor());

            // 3) Click interactable
            if (_button != null)
            {
                _button.interactable = _contributor.IsClickable;
            }

            // 4) Layout
            AutoSizeToLines();
        }

        private void UpdateLines(string block)
        {
            if (_linesRoot == null || _linePrefab == null)
            {
                return;
            }

            string[] parts = string.IsNullOrEmpty(block)
                ? Array.Empty<string>()
                : block.Split(new[] { '\n' }, StringSplitOptions.None);

            while (_lines.Count < parts.Length)
            {
                var label = Instantiate(_linePrefab, _linesRoot);
                label.gameObject.SetActive(true);
                _lines.Add(label);
            }

            for (int i = 0; i < _lines.Count; i++)
            {
                bool active = i < parts.Length;
                var label = _lines[i];

                label.gameObject.SetActive(active);

                if (!active)
                {
                    continue;
                }

                string raw = parts[i] ?? string.Empty;
                ApplyFormattingToLabel(raw, label);
                label.ForceMeshUpdate();
            }
        }

        private void ApplyFormattingToLabel(string raw, TextMeshProUGUI label)
        {
            string line = raw;
            label.fontStyle = FontStyles.Normal;

            // Divider line: exactly '---'
            if (line.Trim() == "---")
            {
                label.text = "──────────";
                label.color = _dividerColor;
                return;
            }

            // Warning line: starts with '! '
            if (line.StartsWith("! "))
            {
                line = line.Substring(2);
                label.color = _warningColor;
                label.fontStyle = FontStyles.Bold;
                label.text = line;
                return;
            }

            // Bullet line: starts with '- '
            if (line.StartsWith("- "))
            {
                line = line.Substring(2);
                label.color = _bulletColor;
                label.text = "• " + line;
                return;
            }

            // Default: normal line
            label.color = _normalColor;
            label.text = line;
        }

        private void AutoSizeToLines()
        {
            if (_layoutElement == null || _linesRoot == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();

            var linesRect = (RectTransform)_linesRoot;
            float innerHeight = LayoutUtility.GetPreferredHeight(linesRect);
            float targetHeight = innerHeight + _verticalPadding;

            _layoutElement.preferredHeight = targetHeight;
            _layoutElement.minHeight = targetHeight;

            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        private void ApplyPanelColor(Color c)
        {
            if (_background == null)
            {
                return;
            }

            _background.color = c;
        }

        private void HandleClick()
        {
            if (_contributor == null || !_contributor.IsClickable)
            {
                return;
            }

            _contributor.OnClick();
        }
    }
}
