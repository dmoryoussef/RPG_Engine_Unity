using UnityEngine;

namespace Grass
{
    public enum GrassOrientationMode
    {
        XY_2D, // Sprite-like, quads lie on XY plane, rotate around Z
        XZ_3D  // 3D cards, stand on XZ plane, rotate around Y
    }

    [CreateAssetMenu(menuName = "WorldGrid/Grass/Grass Render Profile", fileName = "GrassRenderProfile")]
    public sealed class GrassRenderProfile : ScriptableObject
    {
        // --------------------------------------------------------------------
        // Orientation / Mesh Selection
        // --------------------------------------------------------------------

        [Header("Orientation")]
        public GrassOrientationMode orientationMode = GrassOrientationMode.XY_2D;

        [Tooltip("Mesh used when Orientation = XY_2D")]
        public Mesh meshXY;

        [Tooltip("Mesh used when Orientation = XZ_3D")]
        public Mesh meshXZ;

        /// <summary>
        /// 0 = XY plane, 1 = XZ plane. Used by shader & renderer to choose axes.
        /// Keep this aligned with orientationMode.
        /// </summary>
        [Range(0f, 1f)]
        public float useXZPlane = 0f;

        public Mesh EffectiveMesh => orientationMode == GrassOrientationMode.XY_2D ? meshXY : meshXZ;

        public void SyncUseXZPlaneFromOrientation()
        {
            useXZPlane = (orientationMode == GrassOrientationMode.XZ_3D) ? 1f : 0f;
        }

        // --------------------------------------------------------------------
        // Mesh Generation Controls (drives the generator buttons)
        // --------------------------------------------------------------------

        [Header("Mesh Generation")]
        [Tooltip("Vertical subdivisions for bending/sway. Higher = smoother curve, more verts.")]
        [Range(1, 16)]
        public int verticalSegments = 8;

        [Tooltip("Adds a 3rd quad at 45°. Disable for a flatter, more sprite-like 2D look.")]
        public bool useThirdQuad = false;

        // --------------------------------------------------------------------
        // Assets
        // --------------------------------------------------------------------

        [Header("Assets")]
        [Tooltip("Template material using the grass shader. Renderer instantiates a runtime copy.")]
        public Material materialTemplate;

        [Tooltip("Optional override for _MainTex on the runtime material.")]
        public Texture2D mainTextureOverride;

        [Tooltip("-1 = keep material default render queue")]
        public int renderQueueOverride = -1;

        // --------------------------------------------------------------------
        // Render
        // --------------------------------------------------------------------

        [Header("Render")]
        public bool castShadows = false;
        public bool receiveShadows = false;

        [Tooltip("Depth offset in world Z for top-down layering (negative draws 'in front' if you use painter style).")]
        public float grassZ = -0.05f;

        // --------------------------------------------------------------------
        // Density / Placement
        // --------------------------------------------------------------------

        [Header("Density")]
        [Range(0f, 8f)] public float clumpsPerTile = 1.5f;
        [Range(0.1f, 10f)] public float clumpsPerTileMultiplier = 2.0f;
        public int maxClumpsPerChunk = 60000;

        [Header("Placement")]
        [Range(0f, 0.45f)] public float tilePadding = 0.12f;

        // --------------------------------------------------------------------
        // Scale
        // --------------------------------------------------------------------

        [Header("Scale")]
        public bool uniformScale = false;

        public float scaleWidthMin = 0.9f;
        public float scaleWidthMax = 1.15f;

        public float scaleHeightMin = 0.95f;
        public float scaleHeightMax = 1.35f;

        [Tooltip("Extra multiplier on height scale for tuning.")]
        public float heightBias = 1.0f;

        // --------------------------------------------------------------------
        // Rotation
        // --------------------------------------------------------------------

        [Header("Rotation")]
        [Tooltip("Base rotation applied to every clump (degrees). 45 is a good default.")]
        public float baseRotationDeg = 45f;

        [Tooltip("Adds a random rotation offset per clump on top of baseRotationDeg.")]
        public bool randomRotation = true;

        [Tooltip("Random rotation offset range (degrees).")]
        public float minRotationDeg = -12f;
        public float maxRotationDeg = 12f;

        // --------------------------------------------------------------------
        // Determinism
        // --------------------------------------------------------------------

        [Header("Determinism")]
        public int globalSeed = 12345;

        // --------------------------------------------------------------------
        // Wind
        // --------------------------------------------------------------------

        [Header("Wind")]
        public Vector3 windDirection = new Vector3(1, 0, 0);
        [Range(0f, 1f)] public float windAmplitude = 0.25f;
        [Range(0f, 8f)] public float windFrequency = 2.0f;
        [Range(0.001f, 0.2f)] public float windWorldScale = 0.03f;

        // --------------------------------------------------------------------
        // Bend / Sway
        // --------------------------------------------------------------------

        [Header("Bend / Sway")]
        [Range(0f, 0.6f)] public float baseStiffness = 0.2f;
        [Range(1f, 6f)] public float bendExponent = 3f;
        [Range(0f, 2f)] public float swayStrength = 1.0f;
        [Range(0f, 2f)] public float tipBoost = 0.5f;

        // --------------------------------------------------------------------
        // Patch Tint
        // --------------------------------------------------------------------

        [Header("Patch Tint")]
        public Color patchColorA = new Color(0.34f, 0.78f, 0.32f, 1f);
        public Color patchColorB = new Color(0.16f, 0.48f, 0.18f, 1f);
        [Range(0.001f, 0.2f)] public float patchScale = 0.04f;
        [Range(0f, 1f)] public float patchStrength = 0.45f;

        // --------------------------------------------------------------------
        // Lighting
        // --------------------------------------------------------------------

        [Header("Lighting / Shadows")]
        [Tooltip("1 = unlit look (stable 2D), 0 = lit look (shadows visible). Requires shader support: _EmissionStrength.")]
        [Range(0f, 1f)]
        public float emissionStrength = 1f;

        // --------------------------------------------------------------------
        // Validation
        // --------------------------------------------------------------------

        public bool IsValid(out string reason)
        {
            if (materialTemplate == null)
            {
                reason = "Missing materialTemplate.";
                return false;
            }

            if (EffectiveMesh == null)
            {
                reason = "Missing mesh for current orientation mode (meshXY or meshXZ).";
                return false;
            }

            reason = null;
            return true;
        }

        private void OnValidate()
        {
            // Keep ranges sane
            verticalSegments = Mathf.Clamp(verticalSegments, 1, 16);
            maxClumpsPerChunk = Mathf.Max(0, maxClumpsPerChunk);

            if (scaleWidthMax < scaleWidthMin) scaleWidthMax = scaleWidthMin;
            if (scaleHeightMax < scaleHeightMin) scaleHeightMax = scaleHeightMin;

            if (maxRotationDeg < minRotationDeg) maxRotationDeg = minRotationDeg;
        }
    }
}
