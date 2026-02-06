using UnityEngine;
using UnityEngine.UI;

namespace WorldGrid.Unity.UI
{
    /// <summary>
    /// Slides a RectTransform on/off screen by shifting anchoredPosition.
    /// No layout or CanvasGroup involvement.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UISlideTogglePanel : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Button toggleButton;
        [SerializeField] private RectTransform panel;

        [Header("Positions")]
        [Tooltip("Anchored position when panel is visible.")]
        [SerializeField] private Vector2 shownPosition = Vector2.zero;

        [Tooltip("Anchored position when panel is hidden (mostly off-screen).")]
        [SerializeField] private Vector2 hiddenPosition = new Vector2(-260f, 0f);

        [Header("State")]
        [SerializeField] private bool startsVisible = true;

        private bool _visible;

        private void Awake()
        {
            if (toggleButton == null || panel == null)
            {
                UnityEngine.Debug.LogError(
                    "UISlideTogglePanel disabled: Missing references.",
                    this);
                enabled = false;
                return;
            }

            _visible = startsVisible;
            applyImmediate(_visible);

            toggleButton.onClick.AddListener(toggle);
        }

        private void OnDestroy()
        {
            if (toggleButton != null)
                toggleButton.onClick.RemoveListener(toggle);
        }

        private void toggle()
        {
            _visible = !_visible;
            applyImmediate(_visible);
        }

        private void applyImmediate(bool visible)
        {
            panel.anchoredPosition = visible ? shownPosition : hiddenPosition;
        }
    }
}
