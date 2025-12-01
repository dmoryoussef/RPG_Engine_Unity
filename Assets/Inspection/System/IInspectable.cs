namespace Inspection
{
    /// <summary>
    /// TUTORIAL:
    /// IInspectable is implemented by objects that can be "looked at" with the inspector.
    ///
    /// It has TWO responsibilities:
    /// - BuildInspectionData: describe how this object should appear in the inspection UI
    /// - OnInspected: react when it is inspected (quests, clues, state changes, etc.)
    ///
    /// NOTE: We separate "describe" from "react" so that:
    /// - BuildInspectionData is mostly "pure" (no side effects)
    /// - OnInspected handles side effects (mark clue discovered, etc.)
    /// </summary>
    public interface IInspectable
    {
        /// <summary>
        /// Fill out the InspectionData struct for this object.
        /// In most cases, this means:
        /// - Set display name
        /// - Set descriptions
        /// - Set icon
        /// - (Later) add interaction actions, clues, stats, etc.
        /// </summary>
        void BuildInspectionData(InspectionContext context, InspectionData data);

        /// <summary>
        /// Called after BuildInspectionData, just before the data is displayed.
        /// Use this for:
        /// - Marking clues as discovered
        /// - Firing events (quest, journal, etc.)
        /// - Playing one-time reactions
        /// </summary>
        void OnInspected(InspectionContext context, InspectionData data);
    }
}
