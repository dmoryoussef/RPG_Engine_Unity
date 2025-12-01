using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Manages/publishes all child HurtBoxes for hit detection (REQUIRED on targets).
    ///
    /// Responsibilities:
    ///  - Collects all child HurtBox components (authoring).
    ///  - Each frame, computes world-space center/radius/half-extents for those HurtBoxes.
    ///  - Pushes those world AABBs into HurtBoxRegistry (broadphase).
    ///
    /// Execution order:
    ///  - Runs before ActionExecutors (DefaultExecutionOrder(50) vs 60), so the
    ///    world data is up-to-date when AttackResolver queries the registry.
    /// </summary>
    [AddComponentMenu("Combat/Hurt Box Manager (Required)")]
    [DefaultExecutionOrder(50)]
    public sealed class HurtBoxManager : MonoBehaviour
    {
        /// <summary>
        /// All HurtBoxes under this manager's hierarchy.
        /// Populated by CollectChildHurtBoxes().
        /// </summary>
        public List<HurtBox> Boxes = new();

        [ContextMenu("Collect Child HurtBoxes")]
        void CollectChildHurtBoxes()
        {
            Boxes.Clear();
            Boxes.AddRange(GetComponentsInChildren<HurtBox>(true));
            Debug.Log($"[HurtBoxManager:{name}] Collected {Boxes.Count} HurtBoxes.", this);
        }

        void Reset() => CollectChildHurtBoxes();

        void OnEnable()
        {
            HurtBoxRegistry.Instance.Register(this);
        }

        void OnDisable()
        {
            if (HurtBoxRegistry.HasInstance)
                HurtBoxRegistry.Instance.Unregister(this);
        }

        /// <summary>
        /// LateUpdate publishes world-space HurtBox data and syncs the registry.
        /// This ensures that even if bones/animations move in Update, the
        /// HurtBox world data seen by AttackResolver is correct.
        /// </summary>
        void LateUpdate()
        {
            if (Boxes == null || Boxes.Count == 0)
                return;

            foreach (var hb in Boxes)
            {
                if (hb == null)
                    continue;

                // Use the assigned Bone transform if present; otherwise the HurtBox's own transform.
                var t = hb.Bone ? hb.Bone : hb.transform;

                switch (hb.Shape.Type)
                {
                    case HurtShapeType.Sphere:
                        {
                            // World center = bone * local center
                            Vector3 centerWS = t.TransformPoint(hb.Shape.Sphere.LocalCenter);

                            // Radius scaled by max axis scale
                            var s = t.lossyScale;
                            float maxAbs = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                            float radiusWS = hb.Shape.Sphere.Radius * maxAbs;

                            hb.WorldCenter = centerWS;
                            hb.WorldRadius = radiusWS;
                            hb.WorldHalfExtents = Vector3.one * radiusWS; // useful fallback for AABB queries
                            break;
                        }

                    case HurtShapeType.AABB:
                        {
                            Vector3 centerWS = t.TransformPoint(hb.Shape.Aabb.LocalCenter);
                            var abs = new Vector3(
                                Mathf.Abs(t.lossyScale.x),
                                Mathf.Abs(t.lossyScale.y),
                                Mathf.Abs(t.lossyScale.z)
                            );
                            Vector3 halfWS = Vector3.Scale(hb.Shape.Aabb.HalfExtents, abs);

                            hb.WorldCenter = centerWS;
                            hb.WorldRadius = halfWS.magnitude; // conservative
                            hb.WorldHalfExtents = halfWS;
                            break;
                        }
                }
            }

            // Push updated AABBs into the broadphase.
            HurtBoxRegistry.Instance.SyncAabbCache(this);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Guardrail: auto-collect if empty when editing.
            if (!Application.isPlaying && (Boxes == null || Boxes.Count == 0))
                CollectChildHurtBoxes();
        }
#endif
    }
}
