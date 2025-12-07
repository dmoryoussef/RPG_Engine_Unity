using Targeting;
using UnityEngine;

public sealed class AITargeter : TargeterBase
{
    // Some AI blackboard or brain reference
    [SerializeField] private Transform lookAtTarget;

    protected override void TickTargeter()
    {
        // Example: always lock the "best" target in FoV every few seconds,
        // or use behavior tree to decide when to cycle/clear.

        // Example hover: ray from AI's eyes forward
        if (targetCamera == null)
        {
            // Optional: AI may not use camera; you can construct a ray directly:
            Vector3 origin = centerTransform.position;
            Vector3 dir = GetFacingDirection();
            var ray = new Ray(origin, dir);
            UpdateHoverFromRay(ray);
        }

        // Example: an AI decision somewhere sets a bool "shouldCycleLock"
        // if (shouldCycleLock) CycleLockFromFov();
        // if (shouldClearLock) ClearLock();
    }

    protected override Vector3 GetFacingDirection()
    {
        if (lookAtTarget != null)
        {
            Vector3 dir = lookAtTarget.position - centerTransform.position;
            dir.z = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                return dir.normalized;
        }

        Vector3 forward = centerTransform != null ? centerTransform.right : Vector3.right;
        forward.z = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.right;
    }
}
