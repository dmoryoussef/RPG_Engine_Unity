using UnityEngine;

namespace Inspection
{
    /// <summary>
    /// An IInspector is any GameObject that can inspect IInspectable targets.
    ///
    /// The inspector's responsibilities:
    /// - Provide a Root (the inspecting entity in the scene)
    /// - Build an InspectionContext for an interaction
    /// - Run inspection workflows (Basic vs Detailed)
    ///
    /// Notes:
    /// - "Basic" is intended for hover/preview.
    /// - "Detailed" is intended for locked/selected inspection.
    /// - What "basic" vs "detailed" contains is owned by the inspectable
    ///   via BuildBasicInspectionData vs BuildInspectionData.
    /// </summary>
    public interface IInspector
    {
        /// <summary>
        /// The GameObject that represents this inspector in the scene
        /// (player root, camera rig, etc.).
        /// </summary>
        GameObject Root { get; }

        /// <summary>
        /// Build an InspectionContext that describes the relationship between
        /// inspector and target for a specific inspection event.
        /// </summary>
        InspectionContext BuildContext(IInspectable target, Vector3 hitPoint);

        /// <summary>
        /// Perform a basic inspection (hover/preview).
        /// Expected flow:
        /// - Build context
        /// - target.BuildBasicInspectionData(context, data)
        /// - target.OnBasicInspected(context)
        /// - notify UI
        /// </summary>
        void InspectBasic(IInspectable target, Vector3 hitPoint);

        /// <summary>
        /// Perform a detailed inspection (locked/selected).
        /// Expected flow:
        /// - Build context
        /// - target.BuildInspectionData(context, data)
        /// - target.OnInspected(context)
        /// - notify UI
        /// </summary>
        void InspectDetailed(IInspectable target, Vector3 hitPoint);
    }
}
