using System.Collections.Generic;
using UnityEngine;
using Targeting; // For TargeterComponent, FocusTarget, FocusChange

namespace Interaction
{
    /// <summary>
    /// Base class for anything that can perform interactions (player, AI, etc.).
    ///
    /// Responsibilities:
    /// - Owns the current interaction target.
    /// - Computes gating (range + facing).
    /// - Drives engine hooks on InteractableBase:
    ///     - OnFocusGained / OnFocusLost
    ///     - OnEnterRange / OnExitRange
    ///     - TryInteract(interactor)
    /// - Registers itself in Core.Registry for global access.
    /// </summary>
    public abstract class InteractorBase : MonoBehaviour, IInteractor
    {
        // --------------------------------------------------------------------
        // Targeting
        // --------------------------------------------------------------------

        [Header("Targeting Source (Optional)")]
        [SerializeField, Tooltip("If set, this Interactor will attach to this Targeter and use its CurrentTarget as the interaction target.")]
        protected TargeterBase _targeter;

        [SerializeField, Tooltip("If true, InteractorBase will attempt to find a Targeter from this GameObject in Awake when not explicitly assigned.")]
        protected bool autoFindTargeter = true;

        [SerializeField, Tooltip("If true, InteractorBase subscribes to Targeter.Model.CurrentTargetChanged and caches that as its current target.")]
        protected bool bindToTargeterCurrent = true;

        // --------------------------------------------------------------------
        // Gating configuration
        // --------------------------------------------------------------------

        [Header("Gating")]
        [SerializeField, Tooltip("Maximum distance allowed between interactor origin and target bounds center.")]
        protected float interactMaxDistance = 1.5f;

        [SerializeField, Range(-1f, 1f), Tooltip("Dot threshold for facing; 1 = must look directly at, -1 = no facing restriction.")]
        protected float interactFacingDotThreshold = 0.4f;

        // --------------------------------------------------------------------
        // Target Cache (debug-visible)
        // --------------------------------------------------------------------

        [Header("Target Cache (Read-Only)")]
        [SerializeField] protected InteractableComponent currentTarget;
        [SerializeField] protected InteractableComponent previousTarget;

        [SerializeField, Tooltip("Optional debug label for where currentTarget came from (Targeter, AI, script, etc.).")]
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
        protected readonly HashSet<InteractableComponent> _inRangeInteractables =
            new HashSet<InteractableComponent>();

        /// <summary>
        /// Shared buffer used for non-alloc registry queries.
        /// </summary>
        private static readonly List<InteractableComponent> _allInteractablesBuffer =
            new List<InteractableComponent>(128);

        private int _rangeCheckFrameOffset;

        // --------------------------------------------------------------------
        // Lifecycle
        // --------------------------------------------------------------------

        protected virtual void Awake()
        {
            if (autoFindTargeter && _targeter == null)
            {
                ITargeter targeter = GetComponent<ITargeter>();
                if (_targeter == null)
                {
                    if (targeter is TargeterBase tb)
                    {
                        _targeter = tb;
                    }
                    else
                    {
                        Logging.GameLog.LogWarning(this, "No Targeter component found.");
                    }
                }
            }

            // Stagger range checks so multiple interactors don't all hit the registry on the same frame.
            _rangeCheckFrameOffset = Random.Range(0, Mathf.Max(1, rangeCheckIntervalFrames));
        }

        protected virtual void OnEnable()
        {
            Core.Registry.Register<InteractorBase>(this);

            if (this is IInteractor interactor)
                Core.Registry.Register<IInteractor>(interactor);

            if (bindToTargeterCurrent && _targeter != null && _targeter.Model != null)
            {
                _targeter.Model.CurrentTargetChanged += OnTargeterCurrentTargetChanged;
            }
        }

        protected virtual void OnDisable()
        {
            NotifyInteractablesOfDisable();

            Core.Registry.Unregister<InteractorBase>(this);

            if (this is IInteractor interactor)
                Core.Registry.Unregister<IInteractor>(interactor);

            if (bindToTargeterCurrent && _targeter != null && _targeter.Model != null)
            {
                _targeter.Model.CurrentTargetChanged -= OnTargeterCurrentTargetChanged;
            }
        }

        protected virtual void OnDestroy()
        {
            NotifyInteractablesOfDisable();
        }

        protected virtual void Update()
        {
            InteractorTick();   // drives UpdateRangeContacts, focus, etc.
        }

        // --------------------------------------------------------------------
        // Base per-frame tick
        // --------------------------------------------------------------------

        /// <summary>
        /// Call this from derived Update() implementations (e.g. PlayerInteractor.Update)
        /// to drive base interactor behavior (range checks, etc.).
        /// </summary>
        protected void InteractorTick()
        {
            if (ShouldUpdateRangeThisFrame())
            {
                UpdateRangeContacts();
            }
        }

        private bool ShouldUpdateRangeThisFrame()
        {
            if (rangeCheckIntervalFrames <= 1)
                return true;

            int frame = Time.frameCount + _rangeCheckFrameOffset;
            return (frame % rangeCheckIntervalFrames) == 0;
        }

        /// <summary>
        /// Performs a proximity sweep over all InteractableBase instances registered in Core.Registry
        /// and fires OnEnterRange/OnExitRange hooks as needed.
        /// </summary>
        protected virtual void UpdateRangeContacts()
        {
            Core.Registry.GetAllNonAlloc<InteractableComponent>(_allInteractablesBuffer);

            if (_allInteractablesBuffer.Count == 0)
                return;

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
            if (!interactable)
                return;

            _inRangeInteractables.Remove(interactable);

            if (currentTarget == interactable)
            {
                SetCurrentTarget(null, "InteractableDisabled");
            }
        }

        // --------------------------------------------------------------------
        // Targeter wiring
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
        protected virtual InteractableComponent ResolveInteractableFromFocus(FocusTarget focus)
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
            return root.GetComponent<InteractableComponent>();
        }

        // --------------------------------------------------------------------
        // Gate info helper
        // --------------------------------------------------------------------

        public virtual InteractionGateInfo BuildGateInfo(InteractableComponent target = null)
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
            {
                // Focus-only: old target loses focus.
                currentTarget.OnFocusLost(this);
            }

            previousTarget = currentTarget;
            currentTarget = target;
            currentTargetSource = sourceLabel;

            if (currentTarget != null)
            {
                // Focus-only: new target gains focus.
                currentTarget.OnFocusGained(this);
            }

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

        public virtual bool TryInteract(InteractableComponent target)
        {
            // Default to the cached target if none provided.
            if (!target)
                target = currentTarget;

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

            // NEW: interactor-aware engine call.
            bool ok = target.TryInteract(this);

            OnInteractionPerformed(target, ok, dist, dot);
            return ok;
        }

        // --------------------------------------------------------------------
        // Gating helpers
        // --------------------------------------------------------------------

        protected bool IsInRange(InteractableComponent target, out float distance)
        {
            distance = 0f;
            if (!target)
                return false;

            Bounds b = target.GetWorldBounds();
            Vector3 origin = GetOrigin();
            Vector3 targetPos = b.center;
            distance = Vector3.Distance(origin, targetPos);
            return distance <= interactMaxDistance;
        }

        protected bool IsFacing(InteractableComponent target, out float dot)
        {
            dot = 1f;
            if (!target)
                return false;

            if (interactFacingDotThreshold <= -1f)
                return true; // disabled

            Bounds b = target.GetWorldBounds();
            Vector3 origin = GetOrigin();
            Vector3 forward = GetFacingDirection();
            Vector3 toTarget = (b.center - origin).normalized;
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
