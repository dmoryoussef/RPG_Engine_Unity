using System.Collections.Generic;
using UnityEngine;
using UI;

namespace Interaction
{
    /// <summary>
    /// Physics-agnostic interaction picker that talks to InteractableBase via RayTest/Bounds.
    /// Subclass to provide origin, facing, and candidate source logic.
    ///
    /// 🔧 HOW TO USE (TUTORIAL):
    /// - Derive a PlayerInteractor (or NPCInteractor) from this class.
    /// - Implement:
    ///     GetOrigin()   → usually player eye/center position.
    ///     GetFacingDir()→ normalized world direction (e.g., from movement or aim).
    /// - In your player Update():
    ///     1) Call UpdateCurrentTarget() every frame to:
    ///         - pick the best InteractableBase
    ///         - fire OnEnterRange / OnLeaveRange / focus hooks
    ///         - update debug fields
    ///     2) When the player presses the interact key:
    ///         - Call TryInteract().
    ///
    /// This class does NOT depend on physics colliders or rigidbodies.
    /// It uses InteractableBase.RayTest() + Bounds, so objects are
    /// "interactable" even with no Collider2D present.
    /// </summary>
    public abstract class InteractorBase : MonoBehaviour, IInteractor
    {
        public enum ProbeMode { Ray, Circle }
        public enum SelectionSort { Nearest, First, BestFacing }

        [Header("Probe Settings")]
        [SerializeField] protected ProbeMode probeMode = ProbeMode.Ray;
        [SerializeField] protected float rayDistance = 1.25f;
        [SerializeField] protected float circleRadius = 0.6f;
        [SerializeField] protected float circleOffset = 0.5f;

        [Header("Selection Rules")]
        [SerializeField] protected SelectionSort selectionSort = SelectionSort.Nearest;
        [Range(-1f, 1f)]
        [SerializeField] protected float minFacingDot = 0.0f;

        [Header("Limits")]
        [SerializeField] protected int maxResults = 8;

        [Header("Runtime Target (Read-Only)")]
        [SerializeField] protected InteractableBase currentTarget;
        [SerializeField] protected float currentTargetDistance = float.MaxValue;
        [SerializeField] protected string currentTargetId = "<none>";

        [Header("Debug Probe")]
        [SerializeField] protected bool drawGizmos = false;
        [SerializeField] protected Vector3 lastOrigin;
        [SerializeField] protected Vector3 lastDir;
        [SerializeField] protected Vector3 lastCircleCenter;
        [SerializeField] protected string lastPicked = "<none>";

        protected readonly List<InteractableBase> _pool = new List<InteractableBase>(128);

        private InteractableBase _previousTarget;

        /// <summary>
        /// Public accessor in case external systems need to inspect the current target.
        /// </summary>
        public InteractableBase CurrentTarget => currentTarget;

        // ---- Subclass must provide these (e.g., player camera/mover data) ----
        protected abstract Vector3 GetOrigin();
        protected abstract Vector3 GetFacingDir(); // normalized world dir

        /// <summary>
        /// Candidate provider.
        /// Override this if you want zone-based or manually-curated sets.
        /// By default, uses the global InteractableRegistry to avoid
        /// FindObjectsOfType and to later plug into a WorldIndex.
        /// </summary>
        protected virtual IEnumerable<InteractableBase> GetCandidates()
        {
            return InteractableRegistry.All;
        }

        // =====================================================================
        //  HIGH-LEVEL API
        // =====================================================================

        /// <summary>
        /// Updates the currentTarget using the same selection rules as TryPick(),
        /// and fires OnEnterRange / OnLeaveRange (and IInteractableFocusable hooks)
        /// when the target changes.
        ///
        /// Call this once per frame from your player/NPC controller.
        /// </summary>
        public void UpdateCurrentTarget()
        {
            _previousTarget = currentTarget;

            if (TryPick(out var picked, out var dist))
            {
                currentTarget = picked;
                currentTargetDistance = dist;
                currentTargetId = picked.InteractableId ?? picked.name;
            }
            else
            {
                currentTarget = null;
                currentTargetDistance = float.MaxValue;
                currentTargetId = "<none>";
            }

            // Range / focus hooks on change
            if (_previousTarget != currentTarget)
            {
                if (_previousTarget != null)
                {
                    _previousTarget.OnLeaveRange();

                    if (_previousTarget is IInteractableFocusable prevFocus)
                    {
                        prevFocus.OnFocusLost();
                    }
                }

                if (currentTarget != null)
                {
                    currentTarget.OnEnterRange();

                    if (currentTarget is IInteractableFocusable newFocus)
                    {
                        newFocus.OnFocusGained();
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to pick a target given current settings (does not invoke it).
        /// This is stateless and will NOT fire range/focus hooks; use
        /// UpdateCurrentTarget() + CurrentTarget for that behavior.
        /// </summary>
        public virtual bool TryPick(out InteractableBase target, out float distance)
        {
            target = null;
            distance = float.MaxValue;

            _pool.Clear();
            foreach (var it in GetCandidates())
            {
                if (!it || !it.isActiveAndEnabled) continue;
                //Debug.Log("Candidate: " + it.name);
                _pool.Add(it);
            }

            Vector3 origin = GetOrigin();
            Vector3 dir = GetFacingDir();
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.up;
            dir.Normalize();

            lastOrigin = origin;
            lastDir = dir;

            switch (probeMode)
            {
                case ProbeMode.Ray:
                    Debug.DrawLine(origin, origin + dir * rayDistance, Color.red, 0.1f);
                    return PickByRay(origin, dir, out target, out distance);

                case ProbeMode.Circle:
                    var center = origin + dir * circleOffset;
                    lastCircleCenter = center;
                    return PickInCircle(center, dir, out target, out distance);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Attempts to interact with the best available target.
        ///
        /// If UpdateCurrentTarget() has been called, that target is used.
        /// Otherwise, a one-off TryPick() is performed.
        /// </summary>
        public virtual bool TryInteract()
        {
            InteractableBase target = currentTarget;

            if (!target)
            {
                if (!TryPick(out target, out _))
                {
                    lastPicked = "<none>";
                    return false;
                }
            }

            lastPicked = target.InteractableId ?? target.name;
            return target.OnInteract();
        }

        // =====================================================================
        //  PICKING IMPLEMENTATIONS
        // =====================================================================

        /// <summary>
        /// Ray-based selection using InteractableBase.RayTest().
        /// Honors rayDistance, minFacingDot, and SelectionPriority.
        /// </summary>
        protected virtual bool PickByRay(
            Vector3 origin,
            Vector3 dir,
            out InteractableBase best,
            out float bestT
        )
        {
            best = null;
            bestT = float.MaxValue;
            float bestPriority = float.NegativeInfinity;

            var ray = new Ray(origin, dir);

            foreach (var it in _pool)
            {
                if (!it.RayTest(ray, out float t)) continue;

                // If rayDistance > 0, enforce a max distance along the ray.
                // If rayDistance <= 0, treat it as "unlimited" for ray picking.
                if (rayDistance > 0f && t > rayDistance) continue;

                // Facing gate
                var to = (it.transform.position - origin).normalized;
                float dot = Vector3.Dot(dir, to);
                if (dot < minFacingDot) continue;

                bool closer = t < bestT ||
                    (Mathf.Approximately(t, bestT) && it.SelectionPriority > bestPriority);

                if (closer)
                {
                    best = it;
                    bestT = t;
                    bestPriority = it.SelectionPriority;
                }
            }

            return best != null;
        }

        /// <summary>
        /// Circle-based selection (useful for radial, non-precision interaction).
        /// Honors circleRadius, minFacingDot, and selectionSort.
        /// </summary>
        protected virtual bool PickInCircle(
            Vector3 center,
            Vector3 dir,
            out InteractableBase best,
            out float bestDist
        )
        {
            best = null;
            bestDist = float.MaxValue;
            float bestScore = float.NegativeInfinity;

            float r2 = circleRadius * circleRadius;

            foreach (var it in _pool)
            {
                var b = it.GetWorldBounds();
                Vector3 p = b.center;
                Vector3 to = p - center;
                float d2 = to.sqrMagnitude;
                if (d2 > r2) continue;

                float dot = Vector3.Dot(dir, to.normalized);
                if (dot < minFacingDot) continue;

                float score = selectionSort switch
                {
                    SelectionSort.First => 0f,
                    SelectionSort.Nearest => -d2,
                    SelectionSort.BestFacing => dot * 10f - d2 * 0.1f,
                    _ => -d2
                };

                if (score > bestScore)
                {
                    bestScore = score;
                    best = it;
                    bestDist = Mathf.Sqrt(d2);
                }
            }

            return best != null;
        }

        // =====================================================================
        //  GIZMOS
        // =====================================================================

        protected virtual void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(
                lastOrigin,
                lastOrigin + lastDir * Mathf.Max(rayDistance, circleOffset)
            );

            if (probeMode == ProbeMode.Circle)
            {
#if UNITY_EDITOR
                UnityEditor.Handles.color = new Color(0f, 1f, 1f, 0.25f);
                UnityEditor.Handles.DrawSolidDisc(lastCircleCenter, Vector3.forward, circleRadius);
#endif
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(lastCircleCenter, circleRadius);
            }
            else
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(lastOrigin, lastOrigin + lastDir * rayDistance);
            }
        }
    }
}
