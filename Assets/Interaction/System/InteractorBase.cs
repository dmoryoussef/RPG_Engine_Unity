using UnityEngine;
using Targeting; // For TargeterComponent, FocusTarget, FocusChange

namespace Interaction
{
    public abstract class InteractorBase : MonoBehaviour, IInteractor
    {
        [Header("Targeting Source (Optional)")]
        [SerializeField, Tooltip("If set, this Interactor will automatically bind to the Targeter and use its CurrentTarget as the interaction target.")]
        protected TargeterComponent targeter;

        [SerializeField, Tooltip("If true, InteractorBase will auto-resolve TargeterComponent from this GameObject in Awake when not explicitly assigned.")]
        protected bool autoFindTargeter = true;

        [SerializeField, Tooltip("If true, InteractorBase subscribes to Targeter.Model.CurrentTargetChanged and caches that as its current target.")]
        protected bool bindToTargeterCurrent = true;

        [Header("Gating")]
        [SerializeField] protected float interactMaxDistance = 1.5f;

        [SerializeField, Range(-1f, 1f)]
        protected float interactFacingDotThreshold = 0.4f;

        [Header("Target Cache (Read-Only)")]
        [SerializeField] protected InteractableBase currentTarget;
        [SerializeField] protected InteractableBase previousTarget;

        [SerializeField, Tooltip("Optional debug label for where currentTarget came from (Targeter, AI, script, etc.).")]
        protected string currentTargetSource;

        [SerializeField, Tooltip("Last FocusTarget received from Targeter, if any.")]
        protected FocusTarget currentFocusTarget;

        [SerializeField] protected float distanceToTarget;
        [SerializeField] protected bool inRange;
        [SerializeField] protected bool facingOk;
        [SerializeField] protected bool canInteract;

        // --------------------------------------------------------------------
        // Lifecycle: standard wiring
        // --------------------------------------------------------------------

        protected virtual void Awake()
        {
            if (autoFindTargeter && targeter == null)
            {
                targeter = GetComponent<TargeterComponent>();
            }
        }

        protected virtual void OnEnable()
        {
            if (bindToTargeterCurrent && targeter != null && targeter.Model != null)
            {
                targeter.Model.CurrentTargetChanged += OnTargeterCurrentTargetChanged;
            }
        }

        protected virtual void OnDisable()
        {
            if (bindToTargeterCurrent && targeter != null && targeter.Model != null)
            {
                targeter.Model.CurrentTargetChanged -= OnTargeterCurrentTargetChanged;
            }
        }

        // --------------------------------------------------------------------
        // Targeter -> Interactor bridge
        // --------------------------------------------------------------------

        private void OnTargeterCurrentTargetChanged(FocusTarget focus)
        {
            currentFocusTarget = focus;
            var interactable = ResolveInteractableFromFocus(focus);
            SetCurrentTarget(interactable, sourceLabel: "Targeter.CurrentTarget");
        }

        /// <summary>
        /// Default resolution from FocusTarget to InteractableBase.
        /// Override if a given game uses a different mapping.
        /// </summary>
        protected virtual InteractableBase ResolveInteractableFromFocus(FocusTarget focus)
        {
            if (focus == null)
                return null;

            var logical = focus.LogicalTarget;
            if (logical == null)
                return null;

            var root = logical.TargetTransform;
            if (!root)
                return null;

            // Default rule: InteractableBase on logical root.
            return root.GetComponent<InteractableBase>();
        }

        public virtual InteractionGateInfo BuildGateInfo(InteractableBase target = null)
        {
            // Default to the cached target if none provided.
            if (!target)
                target = currentTarget;

            if (!target)
                return InteractionGateInfo.Empty;

            bool inRangeLocal = IsInRange(target, out float dist);
            bool facingOkLocal = IsFacing(target, out float dot);
            bool canInteractLocal = inRangeLocal && facingOkLocal;

            return new InteractionGateInfo(
                interactorRoot: gameObject,
                interactableRoot: target.gameObject,
                inRange: inRangeLocal,
                distance: dist,
                maxDistance: interactMaxDistance,
                facingOk: facingOkLocal,
                facingDot: dot,
                facingThreshold: interactFacingDotThreshold,
                canInteract: canInteractLocal,
                lastFailReason: null
            );
        }


        /// <summary>
        /// External systems (Targeter, AI, scripts) can set the current target explicitly.
        /// This is also where focus/range hooks live.
        /// </summary>
        public virtual void SetCurrentTarget(InteractableBase target, string sourceLabel = null)
        {
            if (currentTarget == target && currentTargetSource == sourceLabel)
                return;

            if (currentTarget != null)
            {
                currentTarget.OnLeaveRange();

                if (currentTarget is IInteractableFocusable lostFocus)
                {
                    lostFocus.OnFocusLost();
                }
            }

            previousTarget = currentTarget;
            currentTarget = target;
            currentTargetSource = sourceLabel;

            if (currentTarget != null)
            {
                currentTarget.OnEnterRange();

                if (currentTarget is IInteractableFocusable gainedFocus)
                {
                    gainedFocus.OnFocusGained();
                }
            }

            OnCurrentTargetChanged(previousTarget, currentTarget);
        }

        protected virtual void OnCurrentTargetChanged(InteractableBase oldTarget, InteractableBase newTarget)
        {
            // Override in subclasses if you want UI/etc.
        }

        public void ClearCurrentTarget(string sourceLabel = null)
        {
            SetCurrentTarget(null, sourceLabel);
        }

        // --------------------------------------------------------------------
        // Abstract hooks for gating
        // --------------------------------------------------------------------

        protected abstract Vector3 GetOrigin();
        protected abstract Vector3 GetFacingDirection();

        // --------------------------------------------------------------------
        // Interaction ability
        // --------------------------------------------------------------------

        public bool TryInteract()
        {
            return TryInteract(currentTarget);
        }

        public virtual bool TryInteract(InteractableBase target)
        {
            if (!target)
                return false;

            bool inRangeLocal = IsInRange(target, out float dist);
            bool facingLocal = IsFacing(target, out float dot);
            bool canInteractLocal = inRangeLocal && facingLocal;

            distanceToTarget = dist;
            inRange = inRangeLocal;
            facingOk = facingLocal;
            canInteract = canInteractLocal;

            if (!canInteractLocal)
            {
                OnInteractionBlocked(target, inRangeLocal, facingLocal, dist, dot);
                return false;
            }

            bool ok = target.OnInteract();
            OnInteractionPerformed(target, ok, dist, dot);
            return ok;
        }

        // --------------------------------------------------------------------
        // Gating helpers
        // --------------------------------------------------------------------

        protected bool IsInRange(InteractableBase target, out float distance)
        {
            distance = 0f;
            if (!target)
                return false;

            Vector3 origin = GetOrigin();
            Vector3 targetPos = target.transform.position;
            distance = Vector3.Distance(origin, targetPos);
            return distance <= interactMaxDistance;
        }

        protected bool IsFacing(InteractableBase target, out float dot)
        {
            dot = 0f;
            if (!target)
                return false;

            Vector3 origin = GetOrigin();
            Vector3 toTarget = (target.transform.position - origin);
            if (toTarget.sqrMagnitude < 1e-6f)
            {
                dot = 1f;
                return true;
            }

            Vector3 facing = GetFacingDirection();
            if (facing.sqrMagnitude < 1e-6f)
            {
                dot = 0f;
                return false;
            }

            facing.Normalize();
            toTarget.Normalize();
            dot = Vector3.Dot(facing, toTarget);
            return dot >= interactFacingDotThreshold;
        }

        // Optional debug / logging hooks
        protected virtual void OnInteractionBlocked(
            InteractableBase target,
            bool inRangeLocal,
            bool facingOkLocal,
            float distance,
            float dot)
        { }

        protected virtual void OnInteractionPerformed(
            InteractableBase target,
            bool success,
            float distance,
            float dot)
        { }
    }
}
