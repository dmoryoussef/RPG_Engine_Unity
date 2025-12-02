using UnityEngine;

namespace Interaction
{
    /// <summary>
    /// Immutable snapshot of interaction gating between a specific interactor
    /// and interactable at a given moment.
    ///
    /// This is UI-agnostic data that can be consumed by:
    /// - UI (e.g., InteractionStatusSubpanel via InspectionPanelContext.InteractionInfo)
    /// - Debug overlays
    /// - AI / scripting logic
    /// </summary>
    public readonly struct InteractionGateInfo
    {
        public static readonly InteractionGateInfo Empty = new InteractionGateInfo(
            interactorRoot: null,
            interactableRoot: null,
            inRange: false,
            distance: 0f,
            maxDistance: 0f,
            facingOk: false,
            facingDot: 0f,
            facingThreshold: 0f,
            canInteract: false,
            lastFailReason: null
        );

        public GameObject InteractorRoot { get; }
        public GameObject InteractableRoot { get; }

        public bool HasInteractor => InteractorRoot != null;
        public bool HasInteractable => InteractableRoot != null;

        public bool InRange { get; }
        public float Distance { get; }
        public float MaxDistance { get; }

        public bool FacingOk { get; }
        public float FacingDot { get; }
        public float FacingThreshold { get; }

        public bool CanInteract { get; }

        /// <summary>
        /// Optional last failure reason from the interactable. This may be null if
        /// no interaction attempt has been made yet, or if the failure reason is unknown.
        /// </summary>
        public InteractionFailReason? LastFailReason { get; }

        public InteractionGateInfo(
            GameObject interactorRoot,
            GameObject interactableRoot,
            bool inRange,
            float distance,
            float maxDistance,
            bool facingOk,
            float facingDot,
            float facingThreshold,
            bool canInteract,
            InteractionFailReason? lastFailReason)
        {
            InteractorRoot = interactorRoot;
            InteractableRoot = interactableRoot;

            InRange = inRange;
            Distance = distance;
            MaxDistance = maxDistance;

            FacingOk = facingOk;
            FacingDot = facingDot;
            FacingThreshold = facingThreshold;

            CanInteract = canInteract;
            LastFailReason = lastFailReason;
        }

        public InteractionGateInfo WithFailReason(InteractionFailReason? reason)
        {
            return new InteractionGateInfo(
                interactorRoot: InteractorRoot,
                interactableRoot: InteractableRoot,
                inRange: InRange,
                distance: Distance,
                maxDistance: MaxDistance,
                facingOk: FacingOk,
                facingDot: FacingDot,
                facingThreshold: FacingThreshold,
                canInteract: CanInteract,
                lastFailReason: reason
            );
        }
    }
}
