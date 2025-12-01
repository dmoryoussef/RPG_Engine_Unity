// Stage 1 - HurtBoxes.cs
// Purpose: Author hurt boxes on characters (Sphere/AABB). LateUpdate writes world-space data.
// Future me: add Capsule/OBB later; keep public fields serialized for authoring.

using UnityEngine;

namespace Combat
{
    public enum HurtShapeType { Sphere, AABB }
    [System.Serializable] public struct SphereShape { public Vector3 LocalCenter; public float Radius; }
    [System.Serializable] public struct AABBShape { public Vector3 LocalCenter; public Vector3 HalfExtents; }

    [System.Serializable]
    public struct HurtShape
    {
        public HurtShapeType Type;
        public SphereShape Sphere;
        public AABBShape Aabb;
    }

    [AddComponentMenu("Combat/Hurt Box (Region)")]
    public sealed class HurtBox : MonoBehaviour
    {
        [Header("Region")]
        public string Region = "Torso";
        public Transform Bone;    // optional; defaults to this.transform
        public HurtShape Shape;

        // Debug draw
        [Header("Debug Draw")]
        public bool DebugDrawAlways = false;      // draw even when not selected
        public Color DebugColor = new Color(0f, 0.8f, 1f, 0.35f);
        public Color DebugWire = new Color(0f, 0.9f, 1f, 1f);
        public bool ShowLabel = true;

        // Populated each frame by HurtBoxManager
        [System.NonSerialized] public Vector3 WorldCenter;
        [System.NonSerialized] public float WorldRadius;
        [System.NonSerialized] public Vector3 WorldHalfExtents;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!DebugDrawAlways) return;
            DrawGizmosInternal();
        }

        void OnDrawGizmosSelected()
        {
            // If always drawing, selected just uses same path (keeps code simple)
            if (!DebugDrawAlways) DrawGizmosInternal();
        }

        void DrawGizmosInternal()
        {
            // Try to show an accurate preview using the Bone transform even in edit mode
            var t = Bone ? Bone : transform;

            Vector3 centerWS;
            float radius;
            Vector3 halfWS;

            if (Shape.Type == HurtShapeType.Sphere)
            {
                centerWS = t.TransformPoint(Shape.Sphere.LocalCenter);
                var s = t.lossyScale;
                radius = Shape.Sphere.Radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                Gizmos.color = DebugColor;
                Gizmos.DrawSphere(centerWS, radius);
                Gizmos.color = DebugWire;
                Gizmos.DrawWireSphere(centerWS, radius);
            }
            else
            {
                centerWS = t.TransformPoint(Shape.Aabb.LocalCenter);
                var abs = new Vector3(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.y), Mathf.Abs(t.lossyScale.z));
                halfWS = Vector3.Scale(Shape.Aabb.HalfExtents, abs);
                var size = halfWS * 2f;
                Gizmos.color = DebugColor;
                Gizmos.DrawCube(centerWS, size);
                Gizmos.color = DebugWire;
                Gizmos.DrawWireCube(centerWS, size);
            }

#if UNITY_EDITOR
            if (ShowLabel)
            {
                // Draw a small label slightly above the center
                var handleColor = new Color(DebugWire.r, DebugWire.g, DebugWire.b, 1f);
                UnityEditor.Handles.color = handleColor;
                UnityEditor.Handles.Label(centerWS + Vector3.up * 0.05f, Region);
            }
#endif
        }
#endif
    }
}
