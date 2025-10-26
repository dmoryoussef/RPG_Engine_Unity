using UnityEngine;
using RPG.Foundation;

namespace RPG.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class OpenCloseInteractable : InteractableBase
    {
        [SerializeField] private OpenCloseState openCloseState;
        [SerializeField] private LockState optionalLockState;

        [Header("Auto-Setup")]
        [SerializeField] private bool autoEnsureTrigger = true;
        [SerializeField] private float defaultTriggerRadius = 0.5f;

        protected override void Awake()
        {
            base.Awake();
            if (!openCloseState)
                openCloseState = GetComponent<OpenCloseState>() ?? GetComponentInParent<OpenCloseState>(true);
            if (!optionalLockState)
                optionalLockState = GetComponent<LockState>() ?? GetComponentInParent<LockState>(true);

            if (autoEnsureTrigger) EnsureOwnTriggerCollider();
        }

#if UNITY_EDITOR
        protected override void ValidateExtra()
        {
            if (!openCloseState)
                Debug.Log($"<color=red>[OpenClose]</color> Missing OpenCloseState on '{name}'.");

            if (autoEnsureTrigger) EnsureOwnTriggerCollider();
        }
#endif

        protected override bool DoInteract()
        {
            if (!openCloseState) return false;
            if (optionalLockState && optionalLockState.IsLocked) { Debug.Log("It's locked."); return false; }

            bool ok = openCloseState.TryToggle();
            if (ok) Debug.Log(openCloseState.IsOpen ? "Opened." : "Closed.");
            return ok;
        }

        private void EnsureOwnTriggerCollider()
        {
            // We want a trigger collider on THIS GameObject that is NOT the blocking collider.
            var myColliders = GetComponents<Collider2D>();
            Collider2D trigger = null;

            foreach (var c in myColliders)
            {
                if (c != null && c.isTrigger) { trigger = c; break; }
            }

            // If no trigger yet, add a CircleCollider2D as a small interact zone
            if (!trigger)
            {
                var circle = gameObject.AddComponent<CircleCollider2D>();
                circle.isTrigger = true;
                circle.radius = defaultTriggerRadius;
                trigger = circle;
            }

            // If this trigger accidentally equals the blocking collider, fix it:
            if (openCloseState && trigger == openCloseState.BlockingCollider)
            {
                // Convert our trigger to trigger, and ensure the blocking one stays solid.
                trigger.isTrigger = true;
                openCloseState.BlockingCollider.isTrigger = false;

                // If both ended up the same component (unlikely with above), create a new trigger
                if (!trigger.isTrigger || !openCloseState.BlockingCollider || trigger == openCloseState.BlockingCollider)
                {
                    var extra = gameObject.AddComponent<CircleCollider2D>();
                    extra.isTrigger = true;
                    extra.radius = defaultTriggerRadius;
                }
            }
        }
    }
}
