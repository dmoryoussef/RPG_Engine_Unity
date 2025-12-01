using UnityEngine;

namespace Inspection
{
    /// <summary>
    /// TUTORIAL:
    /// This struct describes the "situation" of an inspection:
    ///
    /// - Who is doing the inspecting (InspectorRoot)
    /// - What is being inspected (TargetRoot)
    /// - Where in the world the inspection ray hit (WorldHitPoint)
    ///
    /// This mirrors patterns from your other systems:
    /// - Combat: Attacker / Target / AttackContext
    /// - Interaction: Interactor / Interactable / InteractorContext
    /// - Inspection: Inspector / Inspectable / InspectionContext
    /// </summary>
    public struct InspectionContext
    {
        public GameObject InspectorRoot;
        public GameObject TargetRoot;
        public Vector3 WorldHitPoint;
    }
}
