using UnityEngine;

namespace UI
{
    /// <summary>
    /// Contract for any runtime system that wants to contribute a row to the user HUD.
    /// The HUD is generic and only cares about:
    /// - Ordering (Priority)
    /// - Placement (InMainPanelList)
    /// - Display text (GetDisplayString)
    /// - Panel color (GetColor)
    /// - Click behavior (OnClick / GetClickTarget)
    /// </summary>
    public interface IHudContributor
    {
        int Priority { get; }

        bool InMainPanelList { get; }

        bool IsClickable { get; }

        HudClickMode ClickMode { get; }

        /// <summary>
        /// Returns the current display string for this HUD entry.
        /// Can be single or multi-line (use '\n' to separate lines).
        /// </summary>
        string GetDisplayString();

        /// <summary>
        /// Returns the desired panel background color for this contributor.
        /// The HUD row will poll this every refresh.
        /// </summary>
        Color GetColor();

        /// <summary>
        /// Returns the GameObject that should be the focus/owner for click actions.
        /// </summary>
        GameObject GetClickTarget();

        /// <summary>
        /// Called when the HUD row is clicked (if IsClickable is true).
        /// </summary>
        void OnClick();
    }
}
