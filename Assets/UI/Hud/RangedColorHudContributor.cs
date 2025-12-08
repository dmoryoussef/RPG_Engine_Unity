using System.Text.RegularExpressions;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Generic HUD contributor that:
    /// - Targets a single Component and uses its ToString() result as the display string.
    /// - Interprets that string as a numeric value in [minValue, maxValue].
    /// - Lerps between two colors based on that value.
    /// - Caches text + color-relevant numeric value and updates them at a configurable interval.
    ///
    /// Intended for things like health, stamina, ammo, etc. where ToString()
    /// returns something like "72", "72/100", or "HP: 72 / 100".
    /// Only the FIRST numeric value in the string is used for the color.
    /// </summary>
    public class RangedColorHudContributor : BasicHudContributor
    {
        [Header("Numeric Range")]
        [SerializeField]
        private float _minValue = 0f;

        [SerializeField]
        private float _maxValue = 100f;

        [Header("Color Range")]
        [SerializeField]
        private Color _minColor = Color.red;

        [SerializeField]
        private Color _maxColor = Color.green;

        [Header("Update Settings")]
        [SerializeField]
        private float _updateInterval = 0.1f;

        private float _nextUpdateTime;
        private string _cachedText = "(null)";
        private float _cachedNumeric;
        private bool _hasNumericValue;

        // Regex: first signed float/int in the string
        private static readonly Regex _firstNumberRegex =
            new Regex(@"[-+]?\d*\.?\d+",
                      RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public override string GetDisplayString()
        {
            UpdateIfNeeded();
            return _cachedText;
        }

        public override Color GetColor()
        {
            // If the base contributor is explicitly overriding color, respect that.
            if (OverridePanelColor)
            {
                return PanelColor;
            }

            UpdateIfNeeded();

            if (!_hasNumericValue)
            {
                // Fall back to base behavior (usually Color.white).
                return base.GetColor();
            }

            float t = Mathf.InverseLerp(_minValue, _maxValue, _cachedNumeric);
            return Color.Lerp(_minColor, _maxColor, t);
        }

        private void UpdateIfNeeded()
        {
            if (Time.time < _nextUpdateTime)
            {
                return;
            }

            _nextUpdateTime = Time.time + _updateInterval;

            // Use the base class' display string as the text source
            // (this is usually targetComponent.ToString()).
            string raw = base.GetDisplayString();
            _cachedText = raw;

            if (string.IsNullOrEmpty(raw))
            {
                _hasNumericValue = false;
                return;
            }

            // Try to parse the entire string first (backwards-compatible).
            if (float.TryParse(raw, out float fullValue))
            {
                _hasNumericValue = true;
                _cachedNumeric = fullValue;

                if (_debugLogging)
                {
                    Debug.Log($"[HUD] {name} parsed full '{raw}' as {fullValue}", this);
                }

                return;
            }

            // If that fails, try to extract the FIRST numeric token.
            var match = _firstNumberRegex.Match(raw);
            if (match.Success && float.TryParse(match.Value, out float extracted))
            {
                _hasNumericValue = true;
                _cachedNumeric = extracted;

                if (_debugLogging)
                {
                    Debug.Log($"[HUD] {name} extracted '{match.Value}' from '{raw}' as {extracted}", this);
                }

                return;
            }

            // No numeric content found
            _hasNumericValue = false;

            if (_debugLogging)
            {
                Debug.LogWarning($"[HUD] RangedColorHudContributor on {name} could not find a numeric value in '{raw}'.", this);
            }
        }
    }
}
