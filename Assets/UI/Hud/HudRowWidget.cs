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
    ///     "! " prefix  => warning color + bold
    ///     "- " prefix  => bullet line ("• " + text)
    ///     "---" line   => divider / dim line
    /// - Optionally applies a background color override supplied by BasicHudContributor.
    /// </summary>
    [RequireComponent(typeof(LayoutElement))]
    public class HudRowWidget : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Transform _linesRoot;
        [SerializeField] private TextMeshProUGUI _linePrefab;
        [SerializeField] private Button _button;
        [SerializeField] private Image _background;

        [Header("Layout")]
        [SerializeField] private LayoutElement _layoutElement;
        [SerializeField] private float _verticalPadding = 4f;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _warningColor = new Color(1f, 0.8f, 0.3f);
        [SerializeField] private Color _dividerColor = new Color(1f, 1f, 1f, 0.4f);
        [SerializeField] private Color _bulletColor = Color.white;

        private readonly List<TextMeshProUGUI> _lines = new List<TextMeshProUGUI>();

        private Func<string> _getLabel;
        private Action _onClicked;
        private bool _isClickable;
        private bool _initialized;

        private void Reset()
        {
            if (_layoutElement == null)
                _layoutElement = GetComponent<LayoutElement>();

            if (_button == null)
                _button = GetComponent<Button>();

            if (_background == null)
                _background = GetComponent<Image>();

            if (_linesRoot == null && transform.childCount > 0)
                _linesRoot = transform.GetChild(0);

            if (_linePrefab == null && _linesRoot != null)
                _linePrefab = _linesRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        private void Awake()
        {
            if (_layoutElement == null)
                _layoutElement = GetComponent<LayoutElement>();
        }

        private void OnEnable()
        {
            if (_initialized)
                Refresh();
        }

        /// <summary>
        /// Initialize this row for a single IHudContributor.
        /// </summary>
        public void InitializeSingle(IHudContributor contributor)
        {
            _getLabel = contributor.GetDisplayString;
            _onClicked = contributor.OnClick;
            _isClickable = contributor.IsClickable;
            _initialized = true;

            // Apply contributor-driven background color if available (and override enabled).
            if (contributor is BasicHudContributor basic && basic.OverridePanelColor)
            {
                ApplyPanelColor(basic.PanelColor);
                // Debug.Log($"[HUD] Applying panel color {basic.PanelColor} from {basic.name}", this);
            }

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(HandleClick);
                _button.interactable = _isClickable;
            }

            Refresh();
        }

        /// <summary>
        /// Called by the HUD root periodically (or per-frame) to update text and layout.
        /// </summary>
        public void Refresh()
        {
            if (!_initialized)
                return;

            string block = _getLabel != null ? _getLabel() : string.Empty;
            UpdateLines(block);

            if (_button != null)
                _button.interactable = _isClickable;

            AutoSizeToLines();
        }

        private void UpdateLines(string block)
        {
            if (_linesRoot == null || _linePrefab == null)
                return;

            string[] parts = string.IsNullOrEmpty(block)
                ? Array.Empty<string>()
                : block.Split(new[] { '\n' }, StringSplitOptions.None);

            // Ensure we have enough label instances
            while (_lines.Count < parts.Length)
            {
                var label = Instantiate(_linePrefab, _linesRoot);
                label.gameObject.SetActive(true);
                _lines.Add(label);
            }

            // Assign text & style
            for (int i = 0; i < _lines.Count; i++)
            {
                bool active = i < parts.Length;
                var label = _lines[i];

                label.gameObject.SetActive(active);

                if (!active)
                    continue;

                string raw = parts[i] ?? string.Empty;
                ApplyFormattingToLabel(raw, label);
                label.ForceMeshUpdate();
            }
        }

        /// <summary>
        /// Interpret simple line-level formatting prefixes and apply to label.
        /// </summary>
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
                return;

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
            if (_background != null)
                _background.color = c;
        }

        private void HandleClick()
        {
            if (!_isClickable)
                return;

            _onClicked?.Invoke();
        }
    }
}
