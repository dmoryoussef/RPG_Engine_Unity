// AttackResolver.cs
// Purpose: Performs swept-sphere collision tests against HurtBoxes
// using the global HurtBoxRegistry for broadphase filtering.
// This version is fully self-contained and instrumented for debugging.

using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    public static class ActionResolver
    {
        /// <summary>
        /// Result record returned per hurtbox hit.
        /// </summary>
        public readonly struct SweepHit
        {
            public readonly GameObject target;
            public readonly Vector3 point;
            public readonly Vector3 normal;
            public readonly string region;
            public readonly float param; // 0..1 along sweep

            public SweepHit(GameObject target, Vector3 point, Vector3 normal, string region, float param)
            {
                this.target = target;
                this.point = point;
                this.normal = normal;
                this.region = region;
                this.param = param;
            }
        }

        /// <summary>
        /// Global debug toggle for AttackResolver. Leave true while we debug, 
        /// then you can turn it off later if logs get too spammy.
        /// </summary>
        public static bool DebugLogging = true;

        /// <summary>
        /// Sweeps a sphere from start→end and returns all HurtBox hits along the way.
        ///
        /// start/end: world-space points of the sphere center path.
        /// radius:    sphere radius in world units.
        /// samples:   discrete sample count along the sweep (>= 1).
        /// </summary>
        public static List<SweepHit> SweptSphereQuery(Vector3 start, Vector3 end, float radius, int samples = 5)
        {
            if (samples < 1) samples = 1;

            var results = new List<SweepHit>(16);
            var seen = new HashSet<HurtBox>(); // avoid duplicate hits on same HurtBox

            // Broadphase: approximate capsule AABB.
            Bounds broad = CapsuleAABB(start, end, radius);

            var candidates = QueryActiveHurtBoxes(broad);

            if (DebugLogging)
            {
                Debug.Log(
                    $"[ActionResolver] SweptSphereQuery start={start}, end={end}, r={radius}, samples={samples}, broadphaseCandidates={candidates.Count}",
                    candidates.Count > 0 ? candidates[0] : null);
            }

            // Sample along the sweep path.
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector3 p = Vector3.Lerp(start, end, t);

                foreach (var hb in candidates)
                {
                    if (hb == null || seen.Contains(hb))
                        continue;

                    // Use the HurtBox's shape to do narrowphase testing.
                    if (hb.IsSphere(out var center, out var hbRadius))
                    {
                        if (SphereSphereOverlap(p, radius, center, hbRadius, out var hitPoint, out var hitNormal))
                        {
                            if (TryMakeHit(hb, hitPoint, hitNormal, t, out var hit))
                            {
                                results.Add(hit);
                                seen.Add(hb);
                            }
                        }
                    }
                    else if (hb.IsAabb(out var aabb))
                    {
                        if (SphereAabbOverlap(p, radius, aabb, out var hitPoint, out var hitNormal))
                        {
                            if (TryMakeHit(hb, hitPoint, hitNormal, t, out var hit))
                            {
                                results.Add(hit);
                                seen.Add(hb);
                            }
                        }
                    }
                }
            }

            results.Sort((a, b) => a.param.CompareTo(b.param));

            if (DebugLogging)
            {
                Debug.Log($"[ActionResolver] SweptSphereQuery final hits={results.Count}.",
                    results.Count > 0 ? results[0].target : null);
            }

            return results;
        }

        // ---------------------------------------------------------------------
        // Narrowphase helpers
        // ---------------------------------------------------------------------

        private static bool SphereSphereOverlap(Vector3 c1, float r1, Vector3 c2, float r2,
                                                out Vector3 hitPoint, out Vector3 hitNormal)
        {
            Vector3 delta = c2 - c1;
            float distSq = delta.sqrMagnitude;
            float radSum = r1 + r2;

            if (distSq > radSum * radSum)
            {
                hitPoint = default;
                hitNormal = default;
                return false;
            }

            float dist = Mathf.Sqrt(distSq);
            if (dist > 1e-5f)
            {
                hitNormal = delta / dist;
                hitPoint = c1 + hitNormal * r1;
            }
            else
            {
                // Centers coincide; pick arbitrary normal.
                hitNormal = Vector3.up;
                hitPoint = c1 + hitNormal * r1;
            }

            return true;
        }

        private static bool SphereAabbOverlap(Vector3 c, float r, Bounds aabb,
                                              out Vector3 hitPoint, out Vector3 hitNormal)
        {
            // Clamp sphere center to AABB to find the closest point.
            Vector3 q = aabb.ClosestPoint(c);
            Vector3 delta = q - c;
            float distSq = delta.sqrMagnitude;

            if (distSq > r * r)
            {
                hitPoint = default;
                hitNormal = default;
                return false;
            }

            // If we're inside the box, choose the axis of minimum penetration as the normal.
            if (distSq < 1e-6f)
            {
                Vector3 local = c - aabb.center;
                Vector3 half = aabb.extents;
                float px = half.x - Mathf.Abs(local.x);
                float py = half.y - Mathf.Abs(local.y);
                float pz = half.z - Mathf.Abs(local.z);

                if (px <= py && px <= pz)
                    hitNormal = new Vector3(Mathf.Sign(local.x), 0f, 0f);
                else if (py <= px && py <= pz)
                    hitNormal = new Vector3(0f, Mathf.Sign(local.y), 0f);
                else
                    hitNormal = new Vector3(0f, 0f, Mathf.Sign(local.z));

                hitPoint = aabb.center + Vector3.Scale(hitNormal, half);
            }
            else
            {
                float dist = Mathf.Sqrt(distSq);
                hitNormal = delta / dist;
                hitPoint = c + hitNormal * r;
            }

            return true;
        }

        private static bool TryMakeHit(HurtBox hb, Vector3 hitPoint, Vector3 hitNormal, float t, out SweepHit hit)
        {
            var targetGO = hb.gameObject;
            hit = new SweepHit(targetGO, hitPoint, hitNormal, hb.Region, t);
            return true;
        }

        // ---------------------------------------------------------------------
        // Capsule broadphase
        // ---------------------------------------------------------------------

        static Bounds CapsuleAABB(Vector3 a, Vector3 b, float r)
        {
            var min = Vector3.Min(a, b) - new Vector3(r, r, r);
            var max = Vector3.Max(a, b) + new Vector3(r, r, r);
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        // ---------------------------------------------------------------------
        // Registry query wrapper
        // ---------------------------------------------------------------------

        private static List<HurtBox> QueryActiveHurtBoxes(Bounds broad)
        {
            var list = new List<HurtBox>(32);

            if (HurtBoxRegistry.HasInstance)
            {
                var entries = HurtBoxRegistry.Instance.Query(broad);
                foreach (var (hb, _) in entries)
                {
                    if (hb != null)
                        list.Add(hb);
                }
            }

            return list;
        }
    }

    // -------------------------------------------------------------------------
    // HurtBoxExtensions — real implementations for world-space access
    // -------------------------------------------------------------------------
    public static class HurtBoxExtensions
    {
        public static bool IsSphere(this HurtBox hb, out Vector3 center, out float radius)
        {
            center = hb.WorldCenter;
            radius = hb.WorldRadius;
            return hb.Shape.Type == HurtShapeType.Sphere;
        }

        public static bool IsAabb(this HurtBox hb, out Bounds worldAabb)
        {
            worldAabb = new Bounds(hb.WorldCenter, hb.WorldHalfExtents * 2f);
            return hb.Shape.Type == HurtShapeType.AABB;
        }
    }
}
