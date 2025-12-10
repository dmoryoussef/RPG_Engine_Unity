using System.Collections.Generic;
using UnityEngine;
using Logging;
using State;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Interaction
{
    /// <summary>
    /// Unified interactable component that:
    /// - Lives directly on a GameObject (no separate base class).
    /// - Implements IInteractable for generic interaction calls.
    /// - Exposes engine-level hooks for InteractorBase:
    ///     OnEnterRange / OnExitRange / OnFocusGained / OnFocusLost / TryInteract.
    /// - Bridges interaction events into a BaseState via StateChangeContext.
    ///
    /// Typical flow:
    ///   InteractorBase
    ///     → OnEnterRange/OnExitRange/OnFocusGained/OnFocusLost/TryInteract
    ///     → InteractableComponent (this)
    ///     → BaseState pre-change hooks + TryStateChange(StateChangeContext)
    ///     → game-specific behavior (doors, NPCs, quests, etc.).
    /// </summary>
    public class InteractableComponent : MonoBehaviour, IInteractable
    {
        // =====================================================================
        //  IDENTITY
        // =====================================================================

        [Header("Identity")]
        [SerializeField, Tooltip("Optional ID used for debugging / analytics / save data. If empty, uses GameObject.name.")]
        private string interactableId = "";

        /// <summary>
        /// Optional identifier for this interactable. If empty, uses GameObject.name.
        /// </summary>
        public string InteractableId => string.IsNullOrEmpty(interactableId) ? name : interactableId;

        // =====================================================================
        //  INTERACTION CONFIGURATION
        // =====================================================================

        [Header("Interaction")]
        [SerializeField, Tooltip("If true, this interactable will be disabled (SetActive(false)) on successful interaction.")]
        private bool disableOnSuccess = false;

        [SerializeField, Tooltip("If true, this interactable will be destroyed on successful interaction.")]
        private bool destroyOnSuccess = false;

        [SerializeField, Tooltip("Optional delay (seconds) before destroying on success.")]
        private float destroyDelaySeconds = 0f;

        [SerializeField, Tooltip("Cooldown (seconds) between uses. 0 = no cooldown.")]
        private float cooldownSeconds = 0f;

        [SerializeField, Tooltip("Maximum number of successful uses. 0 = unlimited.")]
        private int maxUses = 0;

        [SerializeField, Tooltip("If true, attempt to soft-validate this interactable on Awake/OnValidate.")]
        private bool autoValidate = true;

        [Header("Interaction Input")]
        [SerializeField]
        private KeyCode interactionKey = KeyCode.E;

        public KeyCode InteractionKey => interactionKey;
        // =====================================================================
        //  STATE TARGET
        // =====================================================================

        [Header("State Target")]
        [Tooltip("State component driven by this interactable (must inherit from State.BaseState).")]
        [SerializeField] private BaseState state;

        // =====================================================================
        //  INTERACTION STATE
        // =====================================================================

        [Header("Interaction State (Read-Only)")]
        [SerializeField] private int usesSoFar = 0;
        [SerializeField] private float lastUseTime = -9999f;
        [SerializeField] private bool lastSuccess = false;
        [SerializeField] private InteractionFailReason lastFailReason = InteractionFailReason.None;

        [Header("Range State (Read-Only)")]
        [SerializeField] private bool isInRange = false;
        [SerializeField] private float lastRangeEnterTime = 0f;

        // =====================================================================
        //  RUNTIME (INTERACTOR) STATE
        // =====================================================================

        [Header("Runtime (Interactor)")]
        [SerializeField, Tooltip("Last interactor that interacted with or focused this.")]
        private InteractorBase _activeInteractor;

        /// <summary>
        /// Interactors that currently consider this interactable in range.
        /// </summary>
        private readonly HashSet<InteractorBase> _interactorsInRange =
            new HashSet<InteractorBase>();

        /// <summary>
        /// The interactor currently associated with this interactable (if any).
        /// Typically the last one that focused or interacted.
        /// </summary>
        public InteractorBase ActiveInteractor => _activeInteractor;

        // =====================================================================
        //  DEBUG / VALIDATION / LOGGING
        // =====================================================================

        [Header("Debug & Validation")]
        [SerializeField] private string validationStatus = "Not validated";
        [SerializeField] private bool validationPassed = true;
        [SerializeField] private bool drawBounds = false;

        [Header("Logging")]
        [SerializeField, Tooltip("If true, logs interaction gates and results via GameLog.")]
        private bool debugLogging = false;

        protected string SystemTag => "Interactable";
        protected bool DebugLogging => debugLogging;

        // =====================================================================
        //  BOUNDS
        // =====================================================================

        [Header("Bounds")]
        [SerializeField, Tooltip("If true, use Renderer.bounds if present. Otherwise, use a local Bounds override.")]
        private bool useRendererBounds = true;

        [SerializeField, Tooltip("Optional local-space bounds override used when useRendererBounds is false or no renderer is available.")]
        private Bounds localBoundsOverride = new Bounds(Vector3.zero, Vector3.one);

        /// <summary>
        /// Returns world-space bounds for interaction / targeting.
        /// </summary>
        public virtual Bounds GetWorldBounds()
        {
            if (useRendererBounds)
            {
                var rend = GetComponentInChildren<Renderer>();
                if (rend != null)
                    return rend.bounds;
            }

            // Fallback to local override.
            var b = localBoundsOverride;
            var center = transform.TransformPoint(b.center);
            var extents = b.extents;
            var right = transform.right * extents.x;
            var up = transform.up * extents.y;
            var forward = transform.forward * extents.z;

            // Approximate world extents magnitude.
            var worldExtents = new Vector3(
                Mathf.Abs(right.x) + Mathf.Abs(up.x) + Mathf.Abs(forward.x),
                Mathf.Abs(right.y) + Mathf.Abs(up.y) + Mathf.Abs(forward.y),
                Mathf.Abs(right.z) + Mathf.Abs(up.z) + Mathf.Abs(forward.z)) * 0.5f;

            return new Bounds(center, worldExtents * 2f);
        }

        // =====================================================================
        //  LIFECYCLE & VALIDATION
        // =====================================================================

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!autoValidate)
                return;

            SoftValidate(isEditorPhase: true);
        }
#endif

        private void Awake()
        {
            if (autoValidate)
            {
                SoftValidate(isEditorPhase: false); // authoritative at runtime
            }
        }

        private void OnEnable()
        {
            // Register under both this concrete type and the IInteractable interface.
            Core.Registry.Register<InteractableComponent>(this);
            Core.Registry.Register<IInteractable>(this);
        }

        private void OnDisable()
        {
            // Clear local runtime state and unregister.
            _interactorsInRange.Clear();
            _activeInteractor = null;
            isInRange = false;

            Core.Registry.Unregister<InteractableComponent>(this);
            Core.Registry.Unregister<IInteractable>(this);
        }

        private void OnDestroy()
        {
            // Nothing additional beyond OnDisable for now.
        }

        /// <summary>
        /// Lightweight validation that does not allocate heavy resources.
        /// Called both in-editor and at runtime; use isEditorPhase to branch.
        /// </summary>
        private void SoftValidate(bool isEditorPhase)
        {
            validationPassed = true;
            validationStatus = "OK";

#if UNITY_EDITOR
            if (isEditorPhase && !Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif

            ValidateExtra(isEditorPhase);
        }

        /// <summary>
        /// Resolve the state reference and report configuration issues.
        /// </summary>
        protected virtual void ValidateExtra(bool isEditorPhase)
        {
            if (state == null)
            {
                // Try auto-resolve if not explicitly assigned.
                state = GetComponent<BaseState>();
            }

            if (state == null && !isEditorPhase)
            {
                GameLog.LogWarning(
                    this,
                    system: SystemTag,
                    action: "ValidateExtra",
                    message:
                        $"No BaseState assigned or found on '{name}'. " +
                        "Assign a component that derives from State.BaseState.");
            }
        }

        // =====================================================================
        //  CONTEXT BUILDING
        // =====================================================================

        /// <summary>
        /// Build a generic, interaction-agnostic StateChangeContext for this interactable.
        /// States can inspect Actor/Owner/Channel/Tag if they care.
        /// </summary>
        private StateChangeContext BuildContext(InteractorBase interactor, string channel)
        {
            return new StateChangeContext
            {
                Actor = interactor ? interactor.gameObject : null,
                Owner = gameObject,
                WorldPosition = transform.position,
                Channel = channel,
                Tag = InteractableId,
            };
        }

        // =====================================================================
        //  ENGINE HOOKS (Interactor-aware)
        // =====================================================================

        public virtual void OnEnterRange(InteractorBase interactor)
        {
            GameLog.Log(this, "OnEnterRange", "Interactor", interactor != null ? interactor.name : "null");

            if (interactor == null)
                return;

            _interactorsInRange.Add(interactor);
            isInRange = true;
            lastRangeEnterTime = Time.realtimeSinceStartup;

            if (state != null)
            {
                var ctx = BuildContext(interactor, channel: "RangeEnter");
                state.OnPreStateChangePotentialEntered(ctx);
            }
        }

        public virtual void OnExitRange(InteractorBase interactor)
        {
            GameLog.Log(this, "OnExitRange", "Interactor", interactor != null ? interactor.name : "null");

            if (interactor == null)
                return;

            _interactorsInRange.Remove(interactor);
            if (_interactorsInRange.Count == 0)
                isInRange = false;

            if (state != null)
            {
                var ctx = BuildContext(interactor, channel: "RangeExit");
                state.OnPreStateChangePotentialExited(ctx);
            }
        }

        public virtual void OnFocusGained(InteractorBase interactor)
        {
            GameLog.Log(this, "OnFocusGained", "Interactor", interactor != null ? interactor.name : "null");

            _activeInteractor = interactor;

            if (interactor != null && state != null)
            {
                var ctx = BuildContext(interactor, channel: "FocusGained");
                state.OnPreStateChangeImminentEntered(ctx);
            }
        }

        public virtual void OnFocusLost(InteractorBase interactor)
        {
            GameLog.Log(this, "OnFocusLost", "Interactor", interactor != null ? interactor.name : "null");

            if (_activeInteractor == interactor)
                _activeInteractor = null;

            if (interactor != null && state != null)
            {
                var ctx = BuildContext(interactor, channel: "FocusLost");
                state.OnPreStateChangeImminentExited(ctx);
            }
        }

        // =====================================================================
        //  CORE INTERACTION ENTRY POINTS
        // =====================================================================

        /// <summary>
        /// IInteractable entry point, for callers that don't have an InteractorBase.
        /// This will run the same interaction pipeline, but with no Actor in context.
        /// </summary>
        public bool OnInteract()
        {
            _activeInteractor = null;
            return RunInteractionPipeline(channel: "Interact");
        }

        /// <summary>
        /// Engine-level interaction entry point from an InteractorBase.
        /// Sets ActiveInteractor and runs the interaction pipeline.
        /// </summary>
        public bool TryInteract(InteractorBase interactor)
        {
            _activeInteractor = interactor;
            return RunInteractionPipeline(channel: "Interact");
        }

        /// <summary>
        /// Shared interaction pipeline used by both OnInteract() and TryInteract().
        /// Handles cooldown / max uses / logging, then calls DoInteract().
        /// </summary>
        private bool RunInteractionPipeline(string channel)
        {
            OnInteractionStarted();

            // Cooldown gate.
            float since = Time.realtimeSinceStartup - lastUseTime;
            if (cooldownSeconds > 0f && since < cooldownSeconds)
            {
                lastSuccess = false;
                lastFailReason = InteractionFailReason.Cooldown;

                if (debugLogging)
                {
                    GameLog.Log(
                        this,
                        system: SystemTag,
                        action: "OnInteract",
                        result: "CooldownBlocked",
                        message: $"since={since:F2}, cooldown={cooldownSeconds:F2}");
                }

                OnInteractionFailed(lastFailReason);
                return false;
            }

            // Uses gate.
            if (maxUses > 0 && usesSoFar >= maxUses)
            {
                lastSuccess = false;
                lastFailReason = InteractionFailReason.OutOfUses;

                if (debugLogging)
                {
                    GameLog.Log(
                        this,
                        system: SystemTag,
                        action: "OnInteract",
                        result: "OutOfUses",
                        message: $"usesSoFar={usesSoFar}, maxUses={maxUses}");
                }

                OnInteractionFailed(lastFailReason);
                return false;
            }

            // Execute concrete interaction behavior.
            bool ok = false;
            try
            {
                ok = DoInteract(channel);
            }
            catch (System.Exception ex)
            {
                ok = false;
                lastFailReason = InteractionFailReason.Other;

                if (debugLogging)
                {
                    GameLog.LogError(
                        this,
                        system: SystemTag,
                        action: "OnInteract",
                        message: $"Exception in DoInteract: {ex}");
                }
            }

            lastSuccess = ok;

            if (ok)
            {
                usesSoFar++;
                lastUseTime = Time.realtimeSinceStartup;
                lastFailReason = InteractionFailReason.None;

                if (debugLogging)
                {
                    GameLog.Log(
                        this,
                        system: SystemTag,
                        action: "OnInteract",
                        result: "Success",
                        message: $"usesSoFar={usesSoFar}, cooldownSeconds={cooldownSeconds:F2}");
                }

                OnInteractionSucceeded();

                if (disableOnSuccess)
                {
                    gameObject.SetActive(false);
                }

                if (destroyOnSuccess)
                {
                    Destroy(gameObject, Mathf.Max(0f, destroyDelaySeconds));
                }
            }
            else
            {
                if (lastFailReason == InteractionFailReason.None)
                    lastFailReason = InteractionFailReason.Other;

                if (debugLogging)
                {
                    GameLog.Log(
                        this,
                        system: SystemTag,
                        action: "OnInteract",
                        result: "Failed",
                        message: $"reason={lastFailReason}");
                }

                OnInteractionFailed(lastFailReason);
            }

            return ok;
        }

        /// <summary>
        /// Default interaction behavior:
        /// - Build a StateChangeContext from ActiveInteractor and this GameObject.
        /// - Call state.TryStateChange(context).
        /// Override in subclasses if you need custom behavior.
        /// </summary>
        protected virtual bool DoInteract(string channel)
        {
            if (state == null)
            {
                GameLog.LogWarning(
                    this,
                    system: SystemTag,
                    action: "DoInteract",
                    message: "Interaction attempted with no BaseState assigned.");

                lastFailReason = InteractionFailReason.Other;
                return false;
            }

            var ctx = new StateChangeContext
            {
                Actor = _activeInteractor ? _activeInteractor.gameObject : null,
                Owner = gameObject,
                WorldPosition = transform.position,
                Channel = channel,
                Tag = InteractableId,
            };

            var result = state.TryStateChange(ctx);

            if (DebugLogging)
            {
                GameLog.Log(
                    this,
                    system: "Interact",
                    action: "DoInteract",
                    result: result.Status.ToString(),
                    message: result.Message);
            }

            if (!result.IsSuccess && lastFailReason == InteractionFailReason.None)
            {
                lastFailReason = InteractionFailReason.Other;
            }

            return result.IsSuccess;
        }

        // Called by an Interactor when it is being disabled/destroyed,
        // so this interactable can clear references to it.
        internal void OnInteractorDisabled(InteractorBase interactor)
        {
            if (interactor == null)
                return;

            _interactorsInRange.Remove(interactor);

            if (_activeInteractor == interactor)
                _activeInteractor = null;

            if (_interactorsInRange.Count == 0)
                isInRange = false;
        }


        // =====================================================================
        //  INTERACTION HOOKS (override in subclasses if needed)
        // =====================================================================

        protected virtual void OnInteractionStarted() { }
        protected virtual void OnInteractionSucceeded() { }
        protected virtual void OnInteractionFailed(InteractionFailReason reason) { }

        // =====================================================================
        //  GIZMOS
        // =====================================================================

        private void OnDrawGizmosSelected()
        {
            if (!drawBounds) return;
            var b = GetWorldBounds();
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
