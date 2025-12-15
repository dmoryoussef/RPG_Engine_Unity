// MeleeActionExecutor.cs
// Purpose: Concrete melee executor built on ActionExecutorBase.
//  - Performs per-frame swept-sphere queries using ActionResolver
//  - Maps HurtBox hits to IActionTarget roots
//  - Uses MoveDef authored on the base class (ActionExecutorBase.moveDef)

using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    [AddComponentMenu("Combat/Action/Melee Action Executor")]
    [DefaultExecutionOrder(60)]
    public sealed class MeleeActionExecutor : ActionExecutorBase
    {
        [Header("Rig")]
        [Tooltip("Start of the melee sweep (e.g., weapon tip at start of swing).")]
        [SerializeField] private Transform tipStart;

        [Tooltip("End of the melee sweep (e.g., weapon tip at end of swing).")]
        [SerializeField] private Transform tipEnd;

        [Tooltip("Radius of the swept sphere around the tip path.")]
        [SerializeField] private float radius = 0.5f;

        [Tooltip("How many samples along the sweep path.")]
        [SerializeField] private int samples = 5;

        [Header("Standalone (no controller)")]
        [Tooltip("If true, this executor can run standalone when usePipeline == false, gated by holding the base inputKey.")]
        [SerializeField] private bool standaloneUsesHoldGate = true;

        private bool _standaloneHoldGateOpen;

        // ─────────────────────────────────────────────────────────────────────
        // Rig validity
        // ─────────────────────────────────────────────────────────────────────

        protected override bool HasValidRig()
        {
            return tipStart != null && tipEnd != null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Optional standalone gate (only relevant when usePipeline == false)
        // ─────────────────────────────────────────────────────────────────────

        protected override void Update()
        {
            base.Update();

            // Only used by ActionExecutorBase.LateUpdate() standalone mode.
            // If you're controller-driven (usePipeline == true), this has no effect.
            if (!usePipeline && standaloneUsesHoldGate)
            {
                if (InputKey != KeyCode.None)
                    _standaloneHoldGateOpen = Input.GetKey(InputKey); // hold-to-swing
                else
                    _standaloneHoldGateOpen = false;
            }
            else
            {
                _standaloneHoldGateOpen = true; // ungated if not using hold gate
            }
        }

        protected override bool IsLocallyGated()
        {
            return _standaloneHoldGateOpen;
        }

        // If you want melee to start on PRESS of InputKey (typical), the base WantsToStart() already does that.
        // Override WantsToStart() only if you want HOLD-to-start or RELEASE-to-start.

        // ─────────────────────────────────────────────────────────────────────
        // Hit collection
        // ─────────────────────────────────────────────────────────────────────

        protected override IEnumerable<(GameObject target,
                                        IActionTarget targetComponent,
                                        Vector3 point,
                                        Vector3 normal,
                                        string region,
                                        float param)> CollectHits(CombatFrame frame)
        {
            if (!tipStart || !tipEnd)
            {
                if (debugLevel != DebugLevel.Off)
                    Debug.LogWarning("[MeleeExecutor] CollectHits – tipStart or tipEnd is null.", this);
                yield break;
            }

            Vector3 start = tipStart.position;
            Vector3 end = tipEnd.position;

            List<ActionResolver.SweepHit> hits = ActionResolver.SweptSphereQuery(start, end, radius, samples);

            if (debugLevel != DebugLevel.Off)
            {
                Debug.Log(
                    $"[MeleeExecutor:{PrettyName()}] SweptSphereQuery start={start}, end={end}, radius={radius}, samples={samples}, hits={hits.Count}.",
                    this);
            }

            for (int i = 0; i < hits.Count; i++)
            {
                var h = hits[i];

                IActionTarget targetComponent = null;
                if (h.target != null)
                {
                    targetComponent = h.target.GetComponentInParent<IActionTarget>();
                    if (targetComponent == null && debugLevel != DebugLevel.Off)
                    {
                        Debug.LogWarning(
                            $"[MeleeExecutor:{PrettyName()}] Hit HurtBox on {h.target.name} but found no IActionTarget in parents. Did you add ActionTargetRoot?",
                            h.target);
                    }
                }

                yield return (h.target, targetComponent, h.point, h.normal, h.region, h.param);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!tipStart || !tipEnd) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(tipStart.position, tipEnd.position);
            Gizmos.DrawWireSphere(tipStart.position, radius);
            Gizmos.DrawWireSphere(tipEnd.position, radius);
        }
#endif
    }
}
