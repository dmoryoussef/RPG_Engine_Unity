using UnityEngine;

namespace Player
{
    /// <summary>
    /// Responsibility: Read input each frame and move the player in world space.
    /// - Keeps a Facing vector (last non-zero input) for other systems to use.
    /// - Does NOT touch rendering/animation/visuals.
    /// </summary>
    public class PlayerMover2D : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("World units per second. With 32 PPU, 2.0 ? 64 pixels/sec.")]
        public float speed = 2.0f;

        /// <summary>
        /// The last non-zero movement direction. Consumers (e.g., view/interaction)
        /// can read this to know which way the player is currently facing.
        /// Defaults to down so interaction has a sensible initial direction.
        /// </summary>
        public Vector2 Facing { get; private set; } = Vector2.down;

        private void Update()
        {
            // 1) Gather raw input for snappy control (-1, 0, 1 on each axis).
            Vector2 move = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            );

            // 2) Normalize diagonals so NE/NW/SE/SW isn't faster than N/E/S/W.
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            // 3) Apply frame-rate–independent movement.
            if (move.sqrMagnitude > 0f)
            {
                transform.position += (Vector3)(move * speed * Time.deltaTime);

                // 4) Update facing when there is meaningful input.
                Facing = move;
            }
            // If move is zero, we keep the last Facing value as-is.
        }
    }
}
