// [Stage 2] RadialBurstActionExecutor.cs
// Purpose: Area-of-effect radial burst using ActionExecutorBase.
// Future me: This is basically a "ground slam" that hits any HurtBox
// within a radius of a center point during Active.

//using System.Collections.Generic;
//using UnityEngine;

//namespace Combat
//{
//    [AddComponentMenu("Combat/Action/Radial Burst Action Executor")]
//    [DefaultExecutionOrder(60)]
//    public sealed class RadialBurstActionExecutor : ActionExecutorBase
//    {
//        [Header("Burst Settings")]
//        [Tooltip("Center point of the radial burst. Defaults to this transform if not assigned.")]
//        [SerializeField] private Transform center;
//        [SerializeField] private float radius = 2.0f;

//        [Header("Authoring (optional)")]
//        [Tooltip("Optional MoveDef providing phase timings + damage for Active.")]
//        [SerializeField] private MoveDef moveDef;

//        [Header("Standalone Input (for quick tests)")]
//        [SerializeField] private KeyCode burstKey = KeyCode.Mouse1;
//        private bool _isBurstingStandalone;

//        protected override void Awake()
//        {
//            base.Awake();
//            if (!center)
//                center = transform;
//        }

//        // --------------------------------------------------------------------
//        // ActionExecutorBase overrides
//        // --------------------------------------------------------------------
//        protected override bool HasValidRig() => center != null;

//        protected override IEnumerable<(GameObject target,
//                        IActionTarget targetComponent,
//                        Vector3 point,
//                        Vector3 normal,
//                        string region,
//                        float param)>
//        CollectHits(CombatFrame frame)
//        {
//            // Note: start == end -> effectively an overlap sphere
//            Vector3 c = center.position;
//            var hits = ActionResolver.SweptSphereQuery(c, c, radius);

//            for (int i = 0; i < hits.Count; i++)
//            {
//                var h = hits[i];
//                yield return (h.target, h.damageable, h.point, h.normal, h.region, h.param);
//            }
//        }

//        protected override bool IsLocallyGated() => _isBurstingStandalone;

//        protected override MoveDef GetAuthoringMoveDef() => moveDef;

//        protected override void Update()
//        {
//            base.Update();

//            if (usePipeline) return; // controller-driven only

//            // Simple input gate for standalone: hold to keep the AoE "on"
//            if (Input.GetKeyDown(burstKey)) _isBurstingStandalone = true;
//            if (Input.GetKeyUp(burstKey)) _isBurstingStandalone = false;
//        }

//#if UNITY_EDITOR
//        private void OnDrawGizmosSelected()
//        {
//            if (!center) center = transform;

//            Gizmos.color = Color.cyan;
//            Gizmos.DrawWireSphere(center.position, radius);
//        }
//#endif
//    }
//}
