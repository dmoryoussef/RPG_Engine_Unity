using UnityEngine;
using Player; // For PlayerMover2D

namespace Targeting
{
    /// <summary>
    /// Player-controlled implementation of TargeterBase.
    /// Reads keyboard + mouse input and feeds rays + commands into the base.
    /// </summary>
    public sealed class PlayerTargeter : TargeterBase
    {
        [Header("Player Input")]
        [SerializeField] private KeyCode lockFromHoverKey = KeyCode.Q;
        [SerializeField] private KeyCode cycleLockKey = KeyCode.E;
        [SerializeField] private KeyCode clearLockKey = KeyCode.R;

        private PlayerMover2D _mover2D;

        protected override void Awake()
        {
            base.Awake();

            _mover2D = GetComponent<PlayerMover2D>();
            if (_mover2D == null)
            {
                Debug.LogWarning("[Targeting] PlayerTargeter could not find PlayerMover2D on this GameObject. " +
                                 "FOV will fall back to transform.right.");
            }
        }

        protected override void TickTargeter()
        {
            // 1) Hover from mouse ray
            if (targetCamera != null)
            {
                Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
                UpdateHoverFromRay(ray);
            }

            // 2) Lock controls
            if (Input.GetKeyDown(lockFromHoverKey))
                LockFromHover();

            if (Input.GetKeyDown(cycleLockKey))
                CycleLockFromFov();

            if (Input.GetKeyDown(clearLockKey))
                ClearLock();
        }

        protected override Vector3 GetFacingDirection()
        {
            if (_mover2D != null)
            {
                Vector2 f2 = _mover2D.Facing;
                if (f2.sqrMagnitude > 0.0001f)
                    return new Vector3(f2.x, f2.y, 0f).normalized;
            }

            Vector3 forward = centerTransform != null ? centerTransform.right : Vector3.right;
            forward.z = 0f;

            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.right;

            return forward.normalized;
        }

        public override string ToString()
        {
            var m = Model;

            string Format(FocusTarget ft)
            {
                if (ft == null)
                    return "none";

                var label = ft.TargetLabel ?? "unnamed";
                return $"{label} ({ft.Distance:0.0}m)";
            }

            return
                $"Locked target: {{ {Format(m?.Locked)} }}\n" +
                $"Hover target:  {{ {Format(m?.Hover)} }}\n";
        }




    }
}
