using UnityEngine;

namespace RPG.Player
{
    /// <summary>
    /// Responsibility: Own the SpriteRenderer and react visually to movement state.
    /// - Reads Facing from PlayerMover2D.
    /// - Flips the sprite horizontally when moving left/right.
    /// - Future-ready: this is where you'd add animations, color flashes, etc.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public class PlayerSpriteView2D : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("If left null, we'll GetComponent on this GameObject.")]
        public PlayerMover2D mover;

        private SpriteRenderer _renderer;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (mover == null)
                mover = GetComponent<PlayerMover2D>(); // same object by default
        }

        private void LateUpdate()
        {
            if (mover == null || _renderer == null) return;

            // Read player's current facing to decide visual flip.
            // Note: We only flip when there's meaningful horizontal intent.
            Vector2 facing = mover.Facing;

            // Small deadzone prevents flickering when nearly zero.
            if (Mathf.Abs(facing.x) > 0.01f)
            {
                // Flip when looking/moving left.
                _renderer.flipX = facing.x < 0f;
            }

            // Future: choose sprites/animations here based on facing & speed.
            // e.g., idle vs. walk, up/down frames, etc.
        }
    }
}

