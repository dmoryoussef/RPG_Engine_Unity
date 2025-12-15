using System.Collections.Generic;
using UnityEngine;
using Targeting;

namespace Interaction
{
    /// <summary>
    /// Base class for anything that can perform interactions (player, AI, etc.).
    ///
    /// Targeting integration (optional):
    /// - LockedChanged -> drives the interaction target (recommended default)
    /// - HoverChanged  -> optionally drives the interaction target only when unlocked
    ///
    /// NOTE: This class assumes CurrentTarget/CurrentTargetChanged is being removed from the targeting model.
    /// All targeting events are expected to use FocusChange (Previous + Current).
    /// </summary>
    public abstract class InteractorBase : MonoBehaviour, IInteractor
    {
        // --------------------------------------------------------------------
        // Targeting
        // --------------------------------------------------------------------

        [Header("Targeting Source (Optional)")]
        [SerializeField, Tooltip("If set, this Interactor can attach to this Targeter and use its channels as the interaction target.")]
        protected TargeterBase _targeter;

        [SerializeField, Tooltip("If true, attempt to auto-find a Targeter from this GameObject in Awake when not explicitly assigned.")]
        protected bool autoFindTargeter = true;

        [SerializeField, Tooltip("If true, subscribe to Targeter.Model.LockedChanged and use it as the interaction target.")]
        protected bool bindToTargeterLocked = true;

        [SerializeField, Tooltip("If true, subscribe to Targeter.Model.HoverChanged and use it as the interaction target ONLY when there is no locked target.")]
        protected bool bindToTargeterHoverWhenUnlocked = false;

        // --------------------------------------------------------------------
        // Gating configuration
        // --------------------------------------------------------------------

        [Header("Gating")]
        [SerializeField, Tooltip("Maximum distance allowed between interactor origin and target position.")]
        protected float interactMaxDistance = 1.5f;

        [SerializeField, Range(-1f, 1f), Tooltip("Dot threshold for facing; 1 = must look directly at, -1 = no facing restriction.")]
        protected float interactFacingDotThreshold = 0.4f;

        // --------------------------------------------------------------------
        // Target Cache (debug-visible)
        // --------------------------------------------------------------------

        [Header("Target Cache (Read-Only)")]
        [SerializeField] protected InteractableComponent currentTarget;
        [SerializeField] protected InteractableComponent previousTarget;

        [SerializeField, Tooltip("Optional debug label for where currentTarget came from (Targeter.Locked, Targeter.Hover, AI, script...).")]
        protected string currentTargetSource;

        [SerializeField, Tooltip("Last FocusTarget received from Targeter, if any.")]
        protected FocusTarget currentFocusTarget;

        [SerializeField] protected float distanceToTarget;
        [SerializeField] protected bool inRange;
        [SerializeField] protected bool facingOk;
        [SerializeField] protected bool canInteract;

        // --------------------------------------------------------------------
        // Range update configuration
        // --------------------------------------------------------------------

        [Header("Range Update")]
        [SerializeField, Min(1), Tooltip("How often to update range contacts, in frames. 1 = every frame.")]
        private int rangeCheckIntervalFrames = 10;

        /// <summary>
        /// Interactables currently considered in range of this interactor.
        /// </summary>
        protected readonly HashSet<InteractableComponent> _inRangeInteractables = new HashSet<InteractableComponent>();

        /// <summary>
        /// Shared buffer used for non-alloc registry queries.
        /// </summary>
        private static readonly List<InteractableComponent> _allInteractablesBuffer = new List<InteractableComponent>(128);

        private int _rangeCheckFrameOffset;

        // --------------------------------------------------------------------
        // Lifecycle
        // --------------------------------------------------------------------

        protected virtual void Awake()
        {
            if (autoFindTargeter && _targeter == null)
            {
                var t = GetComponent<ITargeter>();
                if (t is TargeterBase tb) _targeter = tb;
                else Logging.GameLog.LogWarning(this, "No Targeter component found.");
            }

            // Stagger range checks so multiple interactors don't all hit the registry on the same frame.
            _rangeCheckFrameOffset = Random.Range(0, Mathf.Max(1, rangeCheckIntervalFrames));
        }

        protected virtual void OnEnable()
        {
            Core.Registry.Register<InteractorBase>(this);
            if (this is IInteractor interactor)
                Core.Registry.Register<IInteractor>(interactor);

            var model = _targeter?.Model;
            if (model == null) return;

            if (bindToTargeterLocked)
                model.LockedChanged += OnTargeterLockedChanged;

            if (bindToTargeterHoverWhenUnlocked)
                model.HoverChanged += OnTargeterHoverChanged;
        }

        protected virtual void OnDisable()
        {
            NotifyInteractablesOfDisable();

            Core.Registry.Unregister<InteractorBase>(this);
            if (this is IInteractor interactor)
                Core.Registry.Unregister<IInteractor>(interactor);

            var model = _targeter?.Model;
            if (model == null) return;

            if (bindToTargeterLocked)
                model.LockedChanged -= OnTargeterLockedChanged;

            if (bindToTargeterHoverWhenUnlocked)
                model.HoverChanged -= OnTargeterHoverChanged;
        }

        protected virtual void OnDestroy()
        {
            NotifyInteractablesOfDisable();
        }

        protected virtual void Update()
        {
            InteractorTick();
        }

        // --------------------------------------------------------------------
        // Base per-frame tick
        // --------------------------------------------------------------------

        /// <summary>
        /// Drives base interactor behavior (range checks, etc.).
        /// Call from derived Update() implementations if they override Update().
        /// </summary>
        protected void InteractorTick()
        {
            if (ShouldUpdateRangeThisFrame())
                UpdateRangeContacts();
        }

        private bool ShouldUpdateRangeThisFrame()
        {
            if (rangeCheckIntervalFrames <= 1)
                return true;

            int frame = Time.frameCount + _rangeCheckFrameOffset;
            return (frame % rangeCheckIntervalFrames) == 0;
        }

        /// <summary>
        /// Performs a proximity sweep over all InteractableComponent instances registered in Core.Registry
        /// and fires OnEnterRange/OnExitRange hooks as needed.
        /// </summary>
        protected virtual void UpdateRangeContacts()
        {
            Core.Registry.GetAllNonAlloc<InteractableComponent>(_allInteractablesBuffer);
            if (_allInteractablesBuffer.Count == 0) return;

            for (int i = 0; i < _allInteractablesBuffer.Count; i++)
            {
                var interactable = _allInteractablesBuffer[i];
                if (!interactable || !interactable.isActiveAndEnabled)
                    continue;

                bool currentlyInRange = IsInRange(interactable, out _);
                bool wasInRange = _inRangeInteractables.Contains(interactable);

                if (currentlyInRange && !wasInRange)
                {
                    _inRangeInteractables.Add(interactable);
                    interactable.OnEnterRange(this);
                }
                else if (!currentlyInRange && wasInRange)
                {
                    _inRangeInteractables.Remove(interactable);
                    interactable.OnExitRange(this);
                }
            }
        }

        private void NotifyInteractablesOfDisable()
        {
            if (_inRangeInteractables.Count > 0)
            {
                var copy = new List<InteractableComponent>(_inRangeInteractables);
                foreach (var interactable in copy)
                {
                    if (!interactable) continue;
                    interactable.OnInteractorDisabled(this);
                }
            }

            _inRangeInteractables.Clear();

            if (currentTarget != null)
            {
                currentTarget.OnFocusLost(this);
                currentTarget = null;
            }
        }

        /// <summary>
        /// Called by an Interactable when it is being disabled/destroyed,
        /// so this interactor can clear references to it.
        /// </summary>
        internal void OnInteractableDisabled(InteractableComponent interactable)
        {
            if (!interactable) return;

            _inRangeInteractables.Remove(interactable);

            if (currentTarget == interactable)
                SetCurrentTarget(null, "InteractableDisabled");
        }

        // --------------------------------------------------------------------
        // Targeter wiring (Locked / Hover) - FocusChange standardized
        // --------------------------------------------------------------------

        private void OnTargeterLockedChanged(FocusChange change)
        {
            var focus = change.Current;

            currentFocusTarget = focus;

            var interactable = ResolveInteractableFromFocus(focus);
            SetCurrentTarget(interactable, "Targeter.Locked");
        }

        private void OnTargeterHoverChanged(FocusChange change)
        {
            // Only use hover as an interaction target when unlocked.
            // Assumes the model exposes Locked as a property; if not, you can remove this guard.
            if (_targeter?.Model?.Locked != null)
                return;

            var focus = change.Current;

            currentFocusTarget = focus;

            var interactable = ResolveInteractableFromFocus(focus);
            SetCurrentTarget(interactable, "Targeter.Hover");
        }

        /// <summary>
        /// Default resolution from FocusTarget to InteractableComponent.
        /// Override if a given game uses a different mapping.
        /// </summary>
        protected virtual InteractableComponent ResolveInteractableFromFocus(FocusTarget focus)
        {
            if (focus == null) return null;

            var root = focus.LogicalTarget?.TargetTransform;
            if (!root) return null;

            return root.GetComponent<InteractableComponent>();
        }

        // --------------------------------------------------------------------
        // Gate info helper
        // --------------------------------------------------------------------

        public virtual InteractionGateInfo BuildGateInfo(InteractableComponent target = null)
        {
            if (!target) target = currentTarget;
            if (!target) return InteractionGateInfo.Empty;

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

        // --------------------------------------------------------------------
        // Target selection & focus
        // --------------------------------------------------------------------

        /// <summary>
        /// External systems (Targeter, AI, scripts) can set the current target explicitly.
        /// This is where focus hooks live. Range hooks are driven by proximity in UpdateRangeContacts().
        /// </summary>
        public virtual void SetCurrentTarget(InteractableComponent target, string sourceLabel = null)
        {
            if (currentTarget == target && currentTargetSource == sourceLabel)
                return;

            if (currentTarget != null)
                currentTarget.OnFocusLost(this);

            previousTarget = currentTarget;
            currentTarget = target;
            currentTargetSource = sourceLabel;

            if (currentTarget != null)
                currentTarget.OnFocusGained(this);

            OnCurrentTargetChanged(previousTarget, currentTarget);
        }

        protected virtual void OnCurrentTargetChanged(InteractableComponent oldTarget, InteractableComponent newTarget)
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

        public virtual bool TryInteract(InteractableComponent interactableTarget)
        {
            if (!interactableTarget)
                interactableTarget = currentTarget;

            if (!interactableTarget)
                return false;

            bool inRangeLocal = IsInRange(interactableTarget, out float dist);
            bool facingLocal = IsFacing(interactableTarget, out float dot);
            bool canInteractLocal = inRangeLocal && facingLocal;

            distanceToTarget = dist;
            inRange = inRangeLocal;
            facingOk = facingLocal;
            canInteract = canInteractLocal;

            if (!canInteractLocal)
            {
                OnInteractionBlocked(interactableTarget, inRangeLocal, facingLocal, dist, dot);
                return false;
            }

            bool ok = interactableTarget.TryInteract(this);

            OnInteractionPerformed(interactableTarget, ok, dist, dot);
            return ok;
        }

        // --------------------------------------------------------------------
        // Gating helpers
        // --------------------------------------------------------------------

        protected bool IsInRange(InteractableComponent target, out float distance)
        {
            distance = 0f;
            if (!target) return false;

            Vector3 origin = GetOrigin();
            Vector3 targetPos = target.transform.position;

            distance = Vector3.Distance(origin, targetPos);
            return distance <= interactMaxDistance;
        }

        protected bool IsFacing(InteractableComponent target, out float dot)
        {
            dot = 1f;
            if (!target) return false;

            // Facing disabled?
            if (interactFacingDotThreshold <= -1f)
                return true;

            Vector3 origin = GetOrigin();
            Vector3 forward = GetFacingDirection();

            Vector3 toTarget = (target.transform.position - origin).normalized;
            dot = Vector3.Dot(forward, toTarget);

            return dot >= interactFacingDotThreshold;
        }

        // --------------------------------------------------------------------
        // Virtual hooks for subclasses / debug
        // --------------------------------------------------------------------

        protected virtual void OnInteractionBlocked(
            InteractableComponent target,
            bool inRangeLocal,
            bool facingOkLocal,
            float distance,
            float dot)
        { }

        protected virtual void OnInteractionPerformed(
            InteractableComponent target,
            bool success,
            float distance,
            float dot)
        { }
    }
}
