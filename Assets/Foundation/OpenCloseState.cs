using UnityEngine;

namespace RPG.Foundation
{
    [DisallowMultipleComponent]
    public class OpenCloseState : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private bool isOpen = false;
        public bool IsOpen => isOpen;

        [Header("Blocking (auto-setup)")]
        [Tooltip("Enabled when CLOSED, disabled when OPEN. Auto-created if missing.")]
        [SerializeField] private Collider2D blockingCollider;
        [SerializeField] private bool manageBlockingCollider = true;
        [SerializeField] private bool autoCreateBlockingCollider = true;

        public Collider2D BlockingCollider => blockingCollider;

        private void Awake()
        {
            EnsureBlockingCollider();
            ApplyBlocking();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureBlockingCollider();
            ApplyBlocking();
        }
#endif

        public bool TryToggle() { isOpen = !isOpen; ApplyBlocking(); return true; }
        public bool TryOpen() { if (isOpen) return false; isOpen = true; ApplyBlocking(); return true; }
        public bool TryClose() { if (!isOpen) return false; isOpen = false; ApplyBlocking(); return true; }

        private void ApplyBlocking()
        {
            if (!manageBlockingCollider || !blockingCollider) return;
            blockingCollider.enabled = !isOpen;
        }

        private void EnsureBlockingCollider()
        {
            if (blockingCollider && blockingCollider.gameObject == gameObject)
            {
                // Make sure it's solid
                blockingCollider.isTrigger = false;
                return;
            }

            if (!autoCreateBlockingCollider) return;

            // Prefer an existing solid collider on this object
            var any = GetComponent<Collider2D>();
            if (any && !any.isTrigger)
            {
                blockingCollider = any;
                return;
            }

            // Otherwise create a BoxCollider2D sized to sprite bounds (fallback to default)
            if (!blockingCollider)
            {
                var box = GetComponent<BoxCollider2D>();
                if (!box) box = gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = false;
                FitBoxToSprite(box);
                blockingCollider = box;
            }
        }

        private void FitBoxToSprite(BoxCollider2D box)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (!sr || sr.sprite == null) return;

            // World-space size from sprite bounds → convert to local by inverse lossy scale
            var b = sr.sprite.bounds; // local-to-sprite
            // Because BoxCollider2D on the same object uses local space, we can use sprite bounds directly
            box.size = b.size;
            box.offset = b.center;
        }
    }
}
