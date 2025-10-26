using UnityEngine;
using UnityEngine.Events;

namespace RPG.Foundation
{
    /// <summary>
    /// Abstract base for all interactables.
    /// Centralizes soft validation, cooldowns, usage limits, and diagnostics.
    /// Child classes only implement DoInteract() and (optionally) ValidateExtra().
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public abstract class InteractableBase : MonoBehaviour,
        IInteractable,
        IInteractablePrioritized,
        IInteractableFocusable,
        IInteractableState
    {
        // ---------- Identity ----------
        [Header("Identity")]
        [SerializeField] private string interactableId = "";
        public string InteractableId => interactableId;

        // ---------- Rules ----------
        [Header("Rules")]
        [Tooltip("Seconds between successful interactions.")]
        [SerializeField] private float cooldownSeconds = 0f;

        [Tooltip("0 = infinite uses.")]
        [SerializeField] private int maxUses = 0;

        [Tooltip("Disable this GameObject after a successful interaction.")]
        [SerializeField] private bool disableOnSuccess = false;

        [Tooltip("Destroy this GameObject after a successful interaction (optional delay).")]
        [SerializeField] private bool destroyOnSuccess = false;

        [SerializeField] private float destroyDelaySeconds = 0f;

        [Tooltip("Higher values win when PlayerInteract must choose between overlapping targets.")]
        [SerializeField] private float selectionPriority = 0f;
        public float SelectionPriority => selectionPriority;

        // ---------- Debug & Validation ----------
        [Header("Debug & Validation")]
        [SerializeField] private bool drawFacingArrow = false;
        [SerializeField] private string validationStatus = "Not validated";
        [SerializeField] private bool validationPassed = true;

        // ---------- Events ----------
        [Header("Events")]
        [SerializeField] private UnityEvent onInteractSuccess;
        [SerializeField] private UnityEvent onCooldownBlocked;
        [SerializeField] private UnityEvent onOutOfUses;

        // ---------- Runtime (read-only) ----------
        [Header("Runtime (Read-Only)")]
        [SerializeField] private int usesSoFar = 0;
        [SerializeField] private float lastUseTime = -999f;
        [SerializeField] private bool lastSuccess;

        // ---------- Cached ----------
        private Collider2D _col;
        private bool _warnedNoCollider, _warnedNotTrigger;

        // ---------- Abstract / Virtuals ----------
        /// <summary>Subclasses implement their specific behavior here.</summary>
        protected abstract bool DoInteract();

        /// <summary>Subclasses may add extra validation (animator params, refs, etc.).</summary>
        protected virtual void ValidateExtra() { }

        // ---------- Unity Lifecycle ----------
#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            _col = GetComponent<Collider2D>();
            SoftValidate();
        }
#endif

        protected virtual void Awake()
        {
            _col = GetComponent<Collider2D>();
            SoftValidate();
        }

        // ---------- Core Interaction ----------
        public bool OnInteract()
        {
            float since = Time.realtimeSinceStartup - lastUseTime;

            if (cooldownSeconds > 0f && since < cooldownSeconds)
            {
                onCooldownBlocked?.Invoke();
                LogRed($"[{GetType().Name}:{InteractableId}] On cooldown ({since:0.00}/{cooldownSeconds:0.00}s).");
                lastSuccess = false;
                return false;
            }

            if (maxUses > 0 && usesSoFar >= maxUses)
            {
                onOutOfUses?.Invoke();
                LogRed($"[{GetType().Name}:{InteractableId}] No uses left (max={maxUses}).");
                lastSuccess = false;
                return false;
            }

            bool ok = DoInteract();
            lastSuccess = ok;
            if (ok)
            {
                usesSoFar++;
                lastUseTime = Time.realtimeSinceStartup;
                onInteractSuccess?.Invoke();

                if (disableOnSuccess)
                    gameObject.SetActive(false);

                if (destroyOnSuccess)
                    Destroy(gameObject, Mathf.Max(0f, destroyDelaySeconds));
            }
            return ok;
        }

        // ---------- Validation ----------
        protected void SoftValidate()
        {
            validationPassed = true;
            validationStatus = "OK";

            if (_col == null)
            {
                validationPassed = false;
                validationStatus = "Missing Collider2D.";
                if (!_warnedNoCollider)
                {
                    _warnedNoCollider = true;
                    LogRed($"[{GetType().Name}:{InteractableId}] {validationStatus} (GameObject: {name})");
                }
            }
            else if (!_col.isTrigger)
            {
                _col.isTrigger = true;
                validationPassed = false;
                validationStatus = "Collider2D was not a Trigger; auto-set true.";
                if (!_warnedNotTrigger)
                {
                    _warnedNotTrigger = true;
                    LogRed($"[{GetType().Name}:{InteractableId}] {validationStatus} (GameObject: {name})");
                }
            }

            ValidateExtra();
        }

        // ---------- Focus Hooks ----------
        public virtual void OnFocusGained() { /* highlight */ }
        public virtual void OnFocusLost() { /* unhighlight */ }

        // ---------- Persistence ----------
        [System.Serializable]
        public struct InteractableState
        {
            public int UsesSoFar;
            public float LastUseTime;
            public bool LastSuccess;
        }

        public virtual object CaptureState() => new InteractableState
        {
            UsesSoFar = usesSoFar,
            LastUseTime = lastUseTime,
            LastSuccess = lastSuccess
        };

        public virtual void ApplyState(object s)
        {
            if (s is InteractableState st)
            {
                usesSoFar = st.UsesSoFar;
                lastUseTime = st.LastUseTime;
                lastSuccess = st.LastSuccess;
            }
        }

        // ---------- Gizmos & Logging ----------
        protected void LogRed(string msg) => Debug.Log($"<color=red>{msg}</color>");

        protected virtual void OnDrawGizmosSelected()
        {
            if (!drawFacingArrow) return;
            Vector3 origin = transform.position;
            Vector3 fwd = transform.up * 0.75f;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin, origin + fwd);
            Gizmos.DrawSphere(origin + fwd, 0.03f);
        }
    }
}
