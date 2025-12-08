using UnityEngine;
using UnityEngine.Events;
using Logging;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Interaction
{
    /// <summary>
    /// Physics-agnostic base for interactables.
    ///
    /// - NO Collider2D or Rigidbody requirement.
    /// - Selection is done via InteractorBase → RayTest → Bounds.IntersectRay.
    /// - Cooldown / max uses handled here.
    /// - UnityEvents and overridable hooks for interaction + range.
    /// </summary>
    public abstract class InteractableBase : MonoBehaviour
    {
        // =====================================================================
        //  IDENTITY
        // =====================================================================

        [Header("Identity")]
        [SerializeField, Tooltip("Optional ID used for debugging / analytics / save data. Leave empty to use GameObject.name.")]
        private string interactableId = "";

        /// <summary>
        /// Optional identifier for this interactable. If empty, use GameObject.name.
        /// </summary>
        public string InteractableId => interactableId;

        // =====================================================================
        //  RULES
        // =====================================================================

        [Header("Rules")]
        [SerializeField, Tooltip("Seconds between successful interactions. 0 = no cooldown.")]
        private float cooldownSeconds = 0f;

        [SerializeField, Tooltip("Maximum number of successful uses. 0 = infinite.")]
        private int maxUses = 0;

        [SerializeField, Tooltip("Disable this GameObject after a successful interaction.")]
        private bool disableOnSuccess = false;

        [SerializeField, Tooltip("Destroy this GameObject after a successful interaction.")]
        private bool destroyOnSuccess = false;

        [SerializeField, Tooltip("Delay before destruction when DestroyOnSuccess is true.")]
        private float destroyDelaySeconds = 0f;

        /// <summary>
        /// Default key this interactable wants the interactor to listen for.
        /// PlayerInteractor inspects this in TryHandleKeyInteractionsForRoot.
        /// </summary>
        public virtual KeyCode InteractionKey => KeyCode.E;

        // =====================================================================
        //  SELECTION & BOUNDS
        // =====================================================================

        [Header("Selection")]
        [SerializeField, Tooltip("Higher wins when choosing between overlapping targets.")]
        private float selectionPriority = 0f;

        /// <summary>
        /// Used by InteractorBase when multiple interactables overlap.
        /// Higher values are preferred.
        /// </summary>
        public float SelectionPriority => selectionPriority;

        [Header("Bounds")]
        [SerializeField, Tooltip("Optional renderer used to derive world-space bounds. If null, we'll auto-find one.")]
        private Renderer boundsRenderer;

        [SerializeField, Tooltip("Manual local center override for bounds. Zero = unused.")]
        private Vector3 manualCenter = Vector3.zero;

        [SerializeField, Tooltip("Manual local size override for bounds. Zero = unused.")]
        private Vector3 manualSize = Vector3.zero;

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

        /// <summary>
        /// Protected accessor so derived classes can opt into the same debug flag.
        /// </summary>
        protected bool DebugLogging => debugLogging;

        private const string SystemTag = "Interactable";

        // =====================================================================
        //  EVENTS
        // =====================================================================

        [Header("Interaction Events")]
        [SerializeField] private UnityEvent onInteractSuccess;
        [SerializeField] private UnityEvent onCooldownBlocked;
        [SerializeField] private UnityEvent onOutOfUses;

        [Header("Range Events")]
        [SerializeField, Tooltip("Fired when an Interactor starts targeting this.")]
        private UnityEvent onEnterRange;

        [SerializeField, Tooltip("Fired when an Interactor stops targeting this.")]
        private UnityEvent onLeaveRange;

        [Header("Failure Events")]
        [SerializeField, Tooltip("Fired on any non-specific interaction failure (including 'Other').")]
        private UnityEvent onInteractFailed;

        // =====================================================================
        //  RUNTIME (READ-ONLY)
        // =====================================================================

        [Header("Runtime (Read-Only)")]
        [SerializeField] private int usesSoFar = 0;
        [SerializeField] private float lastUseTime = -999f;
        [SerializeField] private bool lastSuccess;
        [SerializeField] private InteractionFailReason lastFailReason = InteractionFailReason.None;
        [SerializeField] private bool isInRange;
        [SerializeField] private float lastRangeEnterTime = -999f;

        public bool IsInRange => isInRange;
        public int UsesSoFar => usesSoFar;
        public bool LastSuccess => lastSuccess;
        public InteractionFailReason LastFailReason => lastFailReason;

        // =====================================================================
        //  VALIDATION LIFECYCLE
        // =====================================================================

#if UNITY_EDITOR
        private void OnValidate()
        {
            SoftValidate(isEditorPhase: true);
        }
#endif

        protected virtual void Awake()
        {
            SoftValidate(isEditorPhase: false); // authoritative at runtime
        }

       
        protected virtual void OnEnable()
        {
            // Register under both its concrete base and the interface.
            Core.Registry.Register<InteractableBase>(this);

            if (this is IInteractable interactable)
                Core.Registry.Register<IInteractable>(interactable);
        }

        protected virtual void OnDisable()
        {
            Core.Registry.Unregister<InteractableBase>(this);

            if (this is IInteractable interactable)
                Core.Registry.Unregister<IInteractable>(interactable);
        }

    /// <summary>
    /// Lightweight validation that does not allocate heavy resources.
    /// Called both in-editor and at runtime; use isEditorPhase to branch.
    /// </summary>
    protected void SoftValidate(bool isEditorPhase)
        {
            validationPassed = true;

            ValidateBounds();
            ValidateExtra(isEditorPhase);

            validationStatus = validationPassed ? "OK" : "Has issues";

#if UNITY_EDITOR
            if (isEditorPhase && !Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        private void ValidateBounds()
        {
            if (manualSize != Vector3.zero)
            {
                validationStatus = "Using manual bounds override.";
                return;
            }

            if (!boundsRenderer)
            {
                boundsRenderer = GetComponentInChildren<Renderer>();
            }

            if (!boundsRenderer)
            {
                validationPassed = false;
                validationStatus = "No Renderer or manual bounds; using fallback.";
            }
            else
            {
                validationStatus = "Using Renderer bounds.";
            }
        }

        /// <summary>
        /// Hook for derived classes to extend validation logic.
        /// </summary>
        protected virtual void ValidateExtra(bool isEditorPhase)
        {
            // Default: nothing extra.
        }

        // =====================================================================
        //  CORE INTERACTION ENTRY POINT
        // =====================================================================

        public bool OnInteract()
        {
            // Hook: interaction has been requested
            OnInteractionStarted();

            // Cooldown gate
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

                onCooldownBlocked?.Invoke();
                OnInteractionFailed(lastFailReason);
                return false;
            }

            // Uses gate
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

                onOutOfUses?.Invoke();
                OnInteractionFailed(lastFailReason);
                return false;
            }

            // Execute subclass logic
            bool ok = DoInteract();
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

                onInteractSuccess?.Invoke();
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
                // If subclass didn't specify a reason, default to Other.
                if (lastFailReason == InteractionFailReason.None)
                {
                    lastFailReason = InteractionFailReason.Other;
                }

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
        /// Implement this in derived classes to define the actual interaction behavior.
        /// Return true on success, false on failure.
        /// Set lastFailReason in the failure case to provide more context.
        /// </summary>
        protected abstract bool DoInteract();

        // =====================================================================
        //  RANGE + INTERACTION HOOKS (override or wire via UnityEvents)
        // =====================================================================

        /// <summary>
        /// Called by InteractorBase when this becomes the current target.
        /// Default behavior:
        /// - Marks isInRange and timestamps it.
        /// - Invokes onEnterRange UnityEvent.
        /// </summary>
        public virtual void OnEnterRange()
        {
            isInRange = true;
            lastRangeEnterTime = Time.realtimeSinceStartup;
            onEnterRange?.Invoke();
        }

        /// <summary>
        /// Called each frame while this is the active target.
        /// Default behavior: does nothing.
        /// </summary>
        public virtual void OnStayInRange()
        {
        }

        /// <summary>
        /// Called by InteractorBase when this stops being the current target.
        /// Default behavior:
        /// - Clears isInRange.
        /// - Invokes onLeaveRange UnityEvent.
        /// </summary>
        public virtual void OnLeaveRange()
        {
            isInRange = false;
            onLeaveRange?.Invoke();
        }

        /// <summary>
        /// Called at the start of any interaction attempt, before gates are evaluated.
        /// Default behavior: does nothing; override for custom behavior.
        /// </summary>
        protected virtual void OnInteractionStarted()
        {
        }

        /// <summary>
        /// Called after a successful interaction.
        /// Default behavior: does nothing (UnityEvent fired earlier).
        /// </summary>
        protected virtual void OnInteractionSucceeded()
        {
        }

        /// <summary>
        /// Called after a failed interaction (for any reason).
        /// Default behavior: invokes onInteractFailed UnityEvent.
        /// </summary>
        protected virtual void OnInteractionFailed(InteractionFailReason reason)
        {
            onInteractFailed?.Invoke();
        }

        /// <summary>
        /// Default ray hit-test: Ray vs world-space AABB using Unity's Bounds.IntersectRay.
        /// Override for custom shapes if needed.
        /// </summary>
        public virtual bool RayTest(Ray ray, out float distance)
        {
            var b = GetWorldBounds();
            return b.IntersectRay(ray, out distance);
        }

        /// <summary>
        /// Returns the world-space bounding box used by the custom picker.
        /// - Manual size override, if provided
        /// - Otherwise first Renderer bounds
        /// - Otherwise a small box around the transform
        /// </summary>
        public Bounds GetWorldBounds()
        {
            // Manual override?
            if (manualSize != Vector3.zero)
            {
                var worldCenter = transform.TransformPoint(manualCenter);
                var scaled = Vector3.Scale(manualSize, Abs(transform.lossyScale));
                var b = new Bounds(worldCenter, scaled);
                EnsureMinThickness(ref b);
                return b;
            }

            // Renderer?
            var r = boundsRenderer != null ? boundsRenderer : GetComponentInChildren<Renderer>();
            if (r != null)
            {
                var b = r.bounds;
                EnsureMinThickness(ref b);
                return b;
            }

            // Fallback small box
            var fb = new Bounds(transform.position, new Vector3(0.5f, 0.5f, 0.2f));
            return fb;
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        private static void EnsureMinThickness(ref Bounds b)
        {
            var size = b.size;
            const float minZ = 0.1f;
            if (size.z < minZ)
            {
                size.z = minZ;
                b.size = size;
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (!drawBounds) return;
            var b = GetWorldBounds();
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
