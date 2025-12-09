using Inspection;

namespace UI
{
    /// <summary>
    /// Implement this on world components that want to contribute
    /// states/actions/sections to the inspection panel for their object.
    /// 
    /// Example usages:
    /// - OpenCloseState adds "Open/Close" action + state line.
    /// - LockState adds "Locked/Unlocked" state + "Unlock" action.
    /// - BookComponent adds "Current Page" section + "Next Page" action.
    /// </summary>
    public interface IInspectionPanelContributor
    {
        /// <summary>
        /// Called by the inspection panel when an object is being inspected.
        /// Use the context to register states, actions, and sections.
        /// </summary>
        void ContributeToPanel(InspectionPanelContext context);
    }
}
