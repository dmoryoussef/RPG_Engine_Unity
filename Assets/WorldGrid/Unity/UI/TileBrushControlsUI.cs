using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WorldGrid.Unity.UI
{
    /// <summary>
    /// UI panel for editing TileBrushState parameters (radius, modes, etc).
    /// Stateless UI: reflects and mutates shared brush state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TileBrushControlsUI : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private TileBrushState brushState;

        [Header("Brush Size")]
        [SerializeField] private Slider radiusSlider;
        [SerializeField] private TMP_Text radiusValueLabel;

        [Header("Config")]
        [SerializeField] private int maxRadius = 5;

        private void Awake()
        {
            if (brushState == null || radiusSlider == null)
            {
                UnityEngine.Debug.LogError(
                    "TileBrushControlsUI disabled: Missing references.",
                    this);
                enabled = false;
                return;
            }

            radiusSlider.minValue = 0;
            radiusSlider.maxValue = maxRadius;
            radiusSlider.wholeNumbers = true;

            syncFromState();
            radiusSlider.onValueChanged.AddListener(onRadiusChanged);
        }

        private void OnEnable()
        {
            syncFromState();
        }

        private void OnDestroy()
        {
            radiusSlider.onValueChanged.RemoveListener(onRadiusChanged);
        }

        private void syncFromState()
        {
            radiusSlider.SetValueWithoutNotify(brushState.brushRadius);
            updateLabel(brushState.brushRadius);
        }

        private void onRadiusChanged(float value)
        {
            int radius = Mathf.RoundToInt(value);
            brushState.brushRadius = radius;
            updateLabel(radius);
        }

        private void updateLabel(int radius)
        {
            if (radiusValueLabel != null)
                radiusValueLabel.text = radius.ToString();
        }
    }
}
