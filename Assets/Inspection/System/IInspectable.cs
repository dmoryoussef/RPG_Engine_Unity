namespace Inspection
{
    /// <summary>
    /// IInspectable is implemented by objects that can be inspected.
    ///
    /// Responsibilities:
    /// - Describe how the object should appear in the UI (basic vs detailed)
    /// - React to being inspected (side effects), without needing UI payload access
    /// </summary>
    public interface IInspectable
    {
        // Describe (pure-ish)
        void BuildInspectionData(InspectionContext context, InspectionData data);       // detailed
        void BuildBasicInspectionData(InspectionContext context, InspectionData data);  // hover/preview

        // React (side effects)
        void OnInspected(InspectionContext context);       // detailed
        void OnBasicInspected(InspectionContext context);  // hover/preview
    }
}
