using UnityEngine;

namespace UI
{
    /// <summary>
    /// Abstraction for anything that wants to expose a line of text to the HUD.
    /// Also owns what should happen when the row is clicked.
    /// </summary>
    public interface IHudContributor
    {
        int Priority { get; }
        bool InMainPanelList { get; }
        bool IsClickable { get; }
        HudClickMode ClickMode { get; }

        /// <summary>
        /// Current string value for this HUD entry.
        /// </summary>
        string GetDisplayString();

        /// <summary>
        /// The GameObject that this entry is primarily about.
        /// Used for contextual actions like inspection.
        /// </summary>
        GameObject GetClickTarget();

        /// <summary>
        /// Called by the HUD when the row is clicked.
        /// The contributor is responsible for delegating to the right system.
        /// </summary>
        void OnClick();
    }
}
