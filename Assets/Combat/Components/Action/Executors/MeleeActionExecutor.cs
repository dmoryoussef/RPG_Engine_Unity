// MeleeActionExecutor.cs
// Purpose: Concrete melee executor built on ActionExecutorBase.
//  - Performs per-frame swept-sphere queries using AttackResolver
//  - Logs how many hits it found and which targets they map to
//  - Uses MoveDef for authored damage where available

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

        [Header("Authoring")]
        [Tooltip("Move definition used for this melee action.")]
        [SerializeField] private MoveDef moveDef;

        [Header("Standalone (no controller)")]
        [SerializeField] private bool standaloneUsesInput = true;
        [SerializeField] private KeyCode standaloneAttackKey = KeyCode.Mouse0;

        private bool _isAttackingStandalone;

        // ─────────────────────────────────────────────────────────────────────
        // Required rig checks
        // ─────────────────────────────────────────────────────────────────────

        protected override bool HasValidRig()
        {
            if (!tipStart || !tipEnd)
                return false;
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Standalone control (when not driven by ActionTimelineController)
        // ─────────────────────────────────────────────────────────────────────

        protected override void Update()
        {
            base.Update();

            if (!usePipeline && standaloneUsesInput)
            {
                // Simple standalone gate: hold attack key while swinging.
                _isAttackingStandalone = Input.GetKey(standaloneAttackKey);
            }
        }

        protected override bool IsLocallyGated() => _isAttackingStandalone;

        protected override MoveDef GetAuthoringMoveDef() => moveDef;

        // ─────────────────────────────────────────────────────────────────────
        // Hit collection
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Collect hits by sweeping from tipStart to tipEnd using AttackResolver.
        /// Logs how many hits were returned so we can see if this step is failing.
        /// </summary>
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

            // Uses the new AttackResolver.SweptSphereQuery signature:
            // List<AttackResolver.SweepHit> with fields: target, point, normal, region, param
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
                    // IMPORTANT: We now resolve IActionTarget here (no more h.damageable).
                    targetComponent = h.target.GetComponentInParent<IActionTarget>();
                    if (targetComponent == null && debugLevel != DebugLevel.Off)
                    {
                        Debug.LogWarning(
                            $"[MeleeExecutor:{PrettyName()}] Hit HurtBox on {h.target.name} but found no IActionTarget in parents. Did you add ActionTargetRoot?",
                            h.target);
                    }
                }

                // This tuple shape matches what ActionExecutorBase.ExecuteFrame expects:
                // (GameObject target, IActionTarget targetComponent, Vector3 point, Vector3 normal, string region, float param)
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
