using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

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
    /// - UnityEvents for success/failure, plus overridable virtual hooks.
    ///
    /// 🔧 HOW TO USE (TUTORIAL):
    /// 1. Inherit from InteractableBase.
    /// 2. Implement DoInteract() to perform your logic and return true/false.
    /// 3. Optionally override:
    ///      - OnEnterRange / OnLeaveRange      → show/hide prompts, highlights.
    ///      - OnInteractionSucceeded           → play SFX, send events.
    ///      - OnInteractionFailed(reason)      → show error barks / UI.
    /// 4. In the Inspector:
    ///      - Set cooldownSeconds / maxUses as needed.
    ///      - Wire onInteractSuccess / onCooldownBlocked / onOutOfUses /
    ///        onInteractFailed / onEnterRange / onLeaveRange events.
    /// </summary>
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        /// <summary>
        /// The key this interactable wants the interactor to listen for.
        /// Default is E; override in subclasses if needed.
        /// </summary>
        public virtual KeyCode InteractionKey => KeyCode.E;

        // ----- Identity -----
        [Header("Identity")]
        [SerializeField] private string interactableId = "";
        public string InteractableId => interactableId;

        // ----- Rules -----
        [Header("Rules")]
        [SerializeField, Tooltip("Seconds between successful interactions.")]
        private float cooldownSeconds = 0f;

        [SerializeField, Tooltip("0 = infinite uses.")]
        private int maxUses = 0;

        [SerializeField, Tooltip("Disable this GameObject after a successful interaction.")]
        private bool disableOnSuccess = false;

        [SerializeField, Tooltip("Destroy this GameObject after a successful interaction.")]
        private bool destroyOnSuccess = false;

        [SerializeField, Tooltip("Delay before destruction when DestroyOnSuccess is true.")]
        private float destroyDelaySeconds = 0f;

        // Optional selection priority (used by InteractorBase picker)
        [Header("Selection")]
        [SerializeField, Tooltip("Higher wins when choosing between overlapping targets.")]
        private float selectionPriority = 0f;
        public float SelectionPriority => selectionPriority;

        // ----- Bounds source for ray tests -----
        [Header("Bounds Source")]
        [SerializeField, Tooltip(
            "If set, use this renderer's bounds for ray hit-testing.\n" +
            "If null, auto-pick the first Renderer on this object or its children.\n" +
            "If still null, falls back to a small box around transform.")]
        private Renderer boundsRenderer;

        [SerializeField, Tooltip("Optional manual bounds override (local space).")]
        private Vector3 manualCenter = Vector3.zero;

        [SerializeField, Tooltip("Optional manual bounds size (local space). Leave zero to ignore.")]
        private Vector3 manualSize = Vector3.zero;

        // ----- Debug & Validation -----
        [Header("Debug & Validation")]
        [SerializeField] private string validationStatus = "Not validated";
        [SerializeField] private bool validationPassed = true;
        [SerializeField] private bool drawBounds = false;

        // ----- Events -----
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

        // ----- Runtime (Read-Only) -----
        [Header("Runtime (Read-Only)")]
        [SerializeField] private int usesSoFar = 0;
        [SerializeField] private float lastUseTime = -999f;
        [SerializeField] private bool lastSuccess;

        [SerializeField] private bool isInRange;
        [SerializeField] private float lastRangeEnterTime = -999f;
        [SerializeField] private float lastRangeExitTime = -999f;
        [SerializeField] private float lastInteractTime = -999f;
        [SerializeField] private InteractionFailReason lastFailReason = InteractionFailReason.None;

        // ----- Template contract -----
        /// <summary>
        /// Implement your game logic here.
        /// Return true on success, false on failure.
        /// - Do NOT apply cooldown/uses logic in here; that is handled by OnInteract().
        /// - If you want a specific fail reason, call OnInteractionFailed(reason)
        ///   before returning false.
        /// </summary>
        protected abstract bool DoInteract();

        /// <summary>
        /// Extra validation for subclasses. Called from SoftValidate().
        /// </summary>
        protected virtual void ValidateExtra(bool isEditorPhase) { }

        /// <summary>
        /// Candidate provider.
        /// Override this if you want zone-based or manually-curated sets.
        /// By default, uses the global InteractableRegistry.
        /// </summary>
        protected virtual IEnumerable<InteractableBase> GetCandidates()
        {
            return InteractableRegistry.All;
        }


#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            SoftValidate(isEditorPhase: true);   // quiet/passive in editor
        }
#endif

        protected virtual void Awake()
        {
            SoftValidate(isEditorPhase: false);  // authoritative at runtime
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

                onCooldownBlocked?.Invoke();
                OnInteractionFailed(lastFailReason);
                return false;
            }

            // Uses gate
            if (maxUses > 0 && usesSoFar >= maxUses)
            {
                lastSuccess = false;
                lastFailReason = InteractionFailReason.OutOfUses;

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

                OnInteractionFailed(lastFailReason);
            }

            return ok;
        }

        // =====================================================================
        //  RANGE + INTERACTION HOOKS (override or wire via UnityEvents)
        // =====================================================================

        /// <summary>
        /// Called by InteractorBase when this becomes the current target.
        /// Default behavior:
        /// - Marks isInRange and timestamps it.
        /// - Invokes onEnterRange UnityEvent.
        ///
        /// Override this to: show highlight, show prompt, etc.
        /// </summary>
        public virtual void OnEnterRange()
        {
            isInRange = true;
            lastRangeEnterTime = Time.realtimeSinceStartup;
            onEnterRange?.Invoke();
        }

        /// <summary>
        /// Called by InteractorBase when this stops being the current target.
        /// Default behavior:
        /// - Clears isInRange and timestamps exit.
        /// - Invokes onLeaveRange UnityEvent.
        /// </summary>
        public virtual void OnLeaveRange()
        {
            isInRange = false;
            lastRangeExitTime = Time.realtimeSinceStartup;
            onLeaveRange?.Invoke();
        }

        /// <summary>
        /// Called at the start of any interaction attempt (before gates).
        /// Override to update UI ('Attempting...', etc.) or start FX.
        /// </summary>
        protected virtual void OnInteractionStarted()
        {
            lastInteractTime = Time.realtimeSinceStartup;
            lastFailReason = InteractionFailReason.None;
        }

        /// <summary>
        /// Called after a successful interaction (after DoInteract returns true).
        /// Override to add common success FX, journal notes, etc.
        /// </summary>
        protected virtual void OnInteractionSucceeded()
        {
            // Intentionally empty. Subclasses may override.
        }

        /// <summary>
        /// Called on any failed attempt.
        /// - This is called for both gate failures (cooldown/uses) and
        ///   DoInteract() = false.
        /// - Subclasses can call this manually with a more specific reason
        ///   before returning false from DoInteract().
        /// </summary>
        /// <param name="reason">Why the interaction failed.</param>
        protected virtual void OnInteractionFailed(InteractionFailReason reason)
        {
            lastFailReason = reason;
            onInteractFailed?.Invoke();
        }

        // =====================================================================
        //  RAY TEST / BOUNDS HELPERS
        // =====================================================================

        /// <summary>
        /// Default ray hit-test: Ray vs world-space AABB using Unity's Bounds.IntersectRay.
        /// Override for custom shapes if needed (sprite masks, polygons, etc.).
        /// </summary>
        public virtual bool RayTest(in Ray ray, out float distance)
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
            const float minZ = 0.05f;
            var s = b.size;
            if (s.z < minZ)
            {
                s.z = minZ;
                b.size = s;
            }
        }

        // =====================================================================
        //  VALIDATION
        // =====================================================================

        protected void SoftValidate(bool isEditorPhase)
        {
            validationPassed = true;
            validationStatus = "OK";

#if UNITY_EDITOR
            // Skip prefab assets / prefab stage to avoid false-null refs in editor
            if (isEditorPhase)
            {
                if (!gameObject.scene.IsValid()) return; // prefab asset
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.scene == gameObject.scene) return; // prefab isolation stage
            }
#endif
            // No collider enforcement; purely ray-based selection now.
            ValidateExtra(isEditorPhase);
        }

        protected virtual void OnEnable()
        {
            InteractableRegistry.Register(this);
        }

        protected virtual void OnDisable()
        {
            InteractableRegistry.Unregister(this);
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
