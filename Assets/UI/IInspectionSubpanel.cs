using Inspection;
namespace UI
{
    public interface IInspectionSubpanel
    {
        /// <summary>
        /// Populate the subpanel for the given inspection data and context.
        /// </summary>
        void OnPopulate(InspectionData data, InspectionPanelContext context);

        /// <summary>
        /// Called when the panel is opened (made visible).
        /// </summary>
        void OnOpen();

        /// <summary>
        /// Called when the panel is closed (hidden).
        /// </summary>
        void OnClose();
    }
}


