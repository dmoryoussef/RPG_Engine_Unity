using UnityEngine;

namespace Inspection
{
    /// <summary>
    /// TUTORIAL:
    /// An IInspector is any GameObject that can "look at" or inspect other objects.
    /// In our prototype, the player will have an InspectorComponent that implements this.
    ///
    /// This interface:
    /// - Exposes the root GameObject for this inspector (Root)
    /// - Decides IF we are allowed to inspect a target (CanInspect)
    /// - Builds the context for a particular inspection (BuildContext)
    /// - Performs the actual inspection workflow (Inspect)
    /// </summary>
    public interface IInspector
    {
        /// <summary>
        /// The GameObject that represents this inspector in the scene.
        /// Usually this is the player root or a camera rig.
        /// </summary>
        GameObject Root { get; }

        /// <summary>
        /// Decide whether this inspector is allowed to inspect the given target right now.
        /// Example checks:
        /// - Too far away
        /// - No line of sight
        /// - Player is stunned or in a cutscene
        /// </summary>
        bool CanInspect(IInspectable target, out string reason);

        /// <summary>
        /// Build an InspectionContext that describes the relationship between
        /// inspector and target for a specific inspection event.
        /// </summary>
        InspectionContext BuildContext(IInspectable target, Vector3 hitPoint);

        /// <summary>
        /// High-level operation that:
        /// - Calls CanInspect
        /// - Builds a context
        /// - Asks the target to build its data
        /// - Calls the OnInspected hook
        /// - Sends the result to some display (debug window, UI, etc.)
        /// </summary>
        void Inspect(IInspectable target, Vector3 hitPoint);
    }
}
