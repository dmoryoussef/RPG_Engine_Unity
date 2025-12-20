using UnityEngine;

namespace Player
{
    /// <summary>
    /// PlayerMover2D
    ///
    /// Responsibility:
    /// - Read input each frame and move the actor in world space.
    /// - Maintain authoritative locomotion info for other systems to poll:
    ///     - Facing direction (last non-zero input)
    ///     - CurrentSpeed (world units/sec)
    ///     - LocomotionState (Idle/Walk/Run) based on thresholds
    ///
    /// Non-responsibilities:
    /// - Does NOT choose animations or sprites. (Visual controller polls this.)
    /// - Does NOT manage combat/action phases. (ActionTimelineController does that.)
    /// </summary>
    public sealed class PlayerMover2D : MonoBehaviour
    {
        public enum LocomotionState
        {
            Idle = 0,
            Walk = 1,
            Run = 2
        }

        [Header("Movement")]
        [Tooltip("World units per second. With 32 PPU, 2.0 ~= 64 pixels/sec.")]
        [Min(0f)]
        public float speed = 2.0f;

        [Header("Locomotion Thresholds (owned by mover)")]
        [Tooltip("If CurrentSpeed < walkSpeedThreshold => Idle.")]
        [Min(0f)]
        public float walkSpeedThreshold = 0.1f;

        [Tooltip("If CurrentSpeed >= runSpeedThreshold => Run. Otherwise Walk.")]
        [Min(0f)]
        public float runSpeedThreshold = 3.0f;

        [Tooltip("Optional hysteresis to prevent flicker around thresholds. 0 disables.")]
        [Min(0f)]
        public float hysteresis = 0.0f;

        /// <summary>
        /// The last non-zero movement direction.
        /// Consumers (visuals, interaction, targeting) can read this to know facing.
        /// Defaults to down so systems have a sensible initial direction.
        /// </summary>
        public Vector2 Facing { get; private set; } = Vector2.down;

        /// <summary>
        /// Magnitude of the current velocity in world units/sec.
        /// This is authoritative locomotion speed; visuals should not recompute it.
        /// </summary>
        public float CurrentSpeed { get; private set; }

        /// <summary>
        /// High-level locomotion state derived from CurrentSpeed and thresholds.
        /// </summary>
        public LocomotionState CurrentLocomotion { get; private set; } = LocomotionState.Idle;

        /// <summary>
        /// Convenience: true if Facing.x indicates right.
        /// (If Facing.x is near zero, this is whatever the last meaningful facing was.)
        /// </summary>
        public bool FacingRight
        {
            get
            {
                if (Mathf.Abs(Facing.x) < 0.001f) return true; // default if purely vertical
                return Facing.x > 0f;
            }
        }

        private void Update()
        {
            // 1) Gather raw input for snappy control (-1, 0, 1 on each axis).
            Vector2 moveInput = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            );

            // 2) Normalize diagonals so NE/NW/SE/SW isn't faster than N/E/S/W.
            if (moveInput.sqrMagnitude > 1f)
                moveInput.Normalize();

            // 3) Compute instantaneous velocity (world units/sec).
            Vector2 velocity = moveInput * speed;
            CurrentSpeed = velocity.magnitude;

            // 4) Apply frame-rate independent movement.
            if (moveInput.sqrMagnitude > 0f)
            {
                transform.position += (Vector3)(velocity * Time.deltaTime);

                // 5) Update facing when there is meaningful input.
                Facing = moveInput;
            }
            // If moveInput is zero, we keep the last Facing value as-is.

            // 6) Update locomotion state (ad-hoc state manager owned by mover).
            UpdateLocomotionState(CurrentSpeed);
        }

        private void UpdateLocomotionState(float speedUnitsPerSec)
        {
            // Optional hysteresis creates a deadband to avoid rapid toggling around thresholds.
            float h = hysteresis;

            switch (CurrentLocomotion)
            {
                case LocomotionState.Idle:
                    // Leave Idle when we exceed walk threshold (+h).
                    if (speedUnitsPerSec >= walkSpeedThreshold + h)
                        CurrentLocomotion = (speedUnitsPerSec >= runSpeedThreshold + h) ? LocomotionState.Run : LocomotionState.Walk;
                    break;

                case LocomotionState.Walk:
                    // Drop to Idle when we go below walk threshold (-h).
                    if (speedUnitsPerSec < Mathf.Max(0f, walkSpeedThreshold - h))
                        CurrentLocomotion = LocomotionState.Idle;
                    // Promote to Run when we exceed run threshold (+h).
                    else if (speedUnitsPerSec >= runSpeedThreshold + h)
                        CurrentLocomotion = LocomotionState.Run;
                    break;

                case LocomotionState.Run:
                    // Drop to Walk when we go below run threshold (-h).
                    if (speedUnitsPerSec < Mathf.Max(0f, runSpeedThreshold - h))
                        CurrentLocomotion = (speedUnitsPerSec >= walkSpeedThreshold + h) ? LocomotionState.Walk : LocomotionState.Idle;
                    break;
            }
        }
    }
}
