using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Grass
{
    [DisallowMultipleComponent]
    public sealed class GrassRendererSystem : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Component implementing IGrassSurfaceProvider (e.g., TilemapGrassSurfaceProvider).")]
        public MonoBehaviour surfaceProviderBehaviour;

        public Mesh clumpMesh;
        public Material grassMaterial;
        public Camera targetCamera;

        [Header("View")]
        public GrassViewMode viewMode = GrassViewMode.TopDown;

        [Tooltip("Fixed Z offset for the grass instances.")]
        public float grassZ = -0.05f;

        [Header("Patches")]
        public int patchSizeInTiles = 8;
        [Tooltip("How many patches around camera to keep active.")]
        public int activeRadiusInPatches = 3;

        [Tooltip("Cull patches beyond this many patches (helps clean up when moving far).")]
        public int hardUnloadRadiusInPatches = 6;

        [Header("Instancing")]
        [Tooltip("Unity limit per DrawMeshInstanced call is 1023.")]
        public bool castShadows = false;
        public bool receiveShadows = false;

        [Header("Wind")]
        public Vector3 windDirection = new Vector3(1, 0, 0);
        [Range(0f, 1f)] public float windAmplitude = 0.25f;
        [Range(0f, 8f)] public float windFrequency = 2.0f;

        [Header("Influencers")]
        public int maxInfluencers = 16;

        private IGrassSurfaceProvider _provider;
        private readonly Dictionary<PatchId, GrassPatch> _patches = new();
        private readonly List<GrassInstance> _scratchInstances = new(4096);

        // Influencers (set externally)
        private readonly List<GrassInfluencer> _influencers = new();

        private MaterialPropertyBlock _mpb;

        private static readonly int ID_InfluencerCount = Shader.PropertyToID("_InfluencerCount");
        private static readonly int ID_Influencers = Shader.PropertyToID("_Influencers");
        private static readonly int ID_InfluencerStrength = Shader.PropertyToID("_InfluencerStrength");
        private static readonly int ID_ViewMode = Shader.PropertyToID("_ViewMode");
        private static readonly int ID_WindDir = Shader.PropertyToID("_WindDir");
        private static readonly int ID_WindAmp = Shader.PropertyToID("_WindAmp");
        private static readonly int ID_WindFreq = Shader.PropertyToID("_WindFreq");
        private static readonly int ID_InstTint = Shader.PropertyToID("_InstTint");

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (targetCamera == null) targetCamera = Camera.main;

            _provider = surfaceProviderBehaviour as IGrassSurfaceProvider;
            if (_provider == null && surfaceProviderBehaviour != null)
                Debug.LogError("GrassRendererSystem: surfaceProviderBehaviour does not implement IGrassSurfaceProvider.");
        }

        public void SetInfluencers(List<GrassInfluencer> points)
        {
            _influencers.Clear();
            if (points == null) return;

            int count = Mathf.Min(points.Count, Mathf.Max(0, maxInfluencers));
            for (int i = 0; i < count; i++) _influencers.Add(points[i]);
        }

        private void LateUpdate()
        {
            if (!ValidateReady()) return;

            UpdateActivePatches();
            UploadGlobals();
            DrawVisiblePatches();
        }

        private bool ValidateReady()
        {
            if (_provider == null) return false;
            if (clumpMesh == null || grassMaterial == null) return false;
            if (targetCamera == null) return false;
            patchSizeInTiles = Mathf.Max(1, patchSizeInTiles);
            activeRadiusInPatches = Mathf.Max(0, activeRadiusInPatches);
            hardUnloadRadiusInPatches = Mathf.Max(activeRadiusInPatches, hardUnloadRadiusInPatches);
            return true;
        }

        private void UpdateActivePatches()
        {
            // Determine camera-centered patch based on world position -> "tile coords" approx
            Vector3 camPos = targetCamera.transform.position;

            // Convert world position to "tile coords" by dividing by 1 unit tiles assumption.
            // If your tilemap uses different scaling, this still works as long as provider and renderer share patchSizeInTiles
            // because the provider uses actual tilemap cell coords internally.
            int camTileX = Mathf.FloorToInt(camPos.x);
            int camTileY = Mathf.FloorToInt(camPos.y);

            int camPatchX = FloorDiv(camTileX, patchSizeInTiles);
            int camPatchY = FloorDiv(camTileY, patchSizeInTiles);
            var center = new PatchId(camPatchX, camPatchY);

            // Mark visible set
            var visibleSet = new HashSet<PatchId>();

            for (int dy = -activeRadiusInPatches; dy <= activeRadiusInPatches; dy++)
            {
                for (int dx = -activeRadiusInPatches; dx <= activeRadiusInPatches; dx++)
                {
                    var id = new PatchId(center.x + dx, center.y + dy);
                    visibleSet.Add(id);

                    if (!_patches.TryGetValue(id, out var patch))
                    {
                        patch = new GrassPatch();
                        BuildPatch(id, patch);
                        _patches[id] = patch;
                    }

                    patch.IsVisible = true;
                }
            }

            // Hide others, unload far ones
            var toRemove = new List<PatchId>();
            foreach (var kv in _patches)
            {
                var id = kv.Key;
                var patch = kv.Value;

                if (!visibleSet.Contains(id))
                    patch.IsVisible = false;

                int dist = ChebyshevDistance(center, id);
                if (dist > hardUnloadRadiusInPatches)
                    toRemove.Add(id);
            }

            for (int i = 0; i < toRemove.Count; i++)
                _patches.Remove(toRemove[i]);
        }

        private void BuildPatch(PatchId id, GrassPatch patch)
        {
            _provider.BuildPatchInstances(id, _scratchInstances);
            patch.BuildFromInstances(_scratchInstances, viewMode, grassZ);
        }

        private void UploadGlobals()
        {
            // Wind + mode
            Vector3 wd = windDirection.sqrMagnitude > 0.0001f ? windDirection.normalized : Vector3.right;
            _mpb.SetVector(ID_WindDir, new Vector4(wd.x, wd.y, wd.z, 0f));
            _mpb.SetFloat(ID_WindAmp, windAmplitude);
            _mpb.SetFloat(ID_WindFreq, windFrequency);
            _mpb.SetInt(ID_ViewMode, (int)viewMode);

            // Influencers
            int cap = Mathf.Max(0, maxInfluencers);
            int count = Mathf.Min(_influencers.Count, cap);

            // Unity requires fixed arrays for SetVectorArray/SetFloatArray.
            // We’ll allocate per frame small; if you prefer, pool these arrays.
            var posRad = new Vector4[cap];
            var strength = new float[cap];

            for (int i = 0; i < count; i++)
            {
                var inf = _influencers[i];
                posRad[i] = new Vector4(inf.position.x, inf.position.y, inf.position.z, inf.radius);
                strength[i] = inf.strength;
            }

            _mpb.SetInt(ID_InfluencerCount, count);
            if (cap > 0)
            {
                _mpb.SetVectorArray(ID_Influencers, posRad);
                _mpb.SetFloatArray(ID_InfluencerStrength, strength);
            }
        }

        private void DrawVisiblePatches()
        {
            var shadowMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            foreach (var kv in _patches)
            {
                var patch = kv.Value;
                if (!patch.IsVisible || patch.InstanceCount == 0) continue;

                patch.Draw(clumpMesh, grassMaterial, _mpb, targetCamera, shadowMode, receiveShadows);
            }
        }

        private static int ChebyshevDistance(PatchId a, PatchId b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            return Mathf.Max(dx, dy);
        }

        private static int FloorDiv(int a, int b)
        {
            // Floor division for negatives
            int q = a / b;
            int r = a % b;
            if (r != 0 && ((r > 0) != (b > 0))) q--;
            return q;
        }

        // ---------- Patch storage ----------
        private sealed class GrassPatch
        {
            private const int BatchMax = 1023;

            public bool IsVisible { get; set; }
            public int InstanceCount => _count;

            private int _count;

            private readonly List<Matrix4x4[]> _matBatches = new();
            private readonly List<Vector4[]> _tintBatches = new();
            private readonly List<int> _batchCounts = new();

            public void BuildFromInstances(List<GrassInstance> instances, GrassViewMode mode, float z)
            {
                _matBatches.Clear();
                _tintBatches.Clear();
                _batchCounts.Clear();
                _count = instances.Count;

                int offset = 0;
                while (offset < instances.Count)
                {
                    int n = Mathf.Min(BatchMax, instances.Count - offset);
                    var mats = new Matrix4x4[n];
                    var tints = new Vector4[n];

                    for (int i = 0; i < n; i++)
                    {
                        var inst = instances[offset + i];

                        Vector3 pos = inst.position;
                        pos.z = z;

                        Quaternion rot = mode == GrassViewMode.TopDown
                            ? Quaternion.AngleAxis(inst.rotationRad * Mathf.Rad2Deg, Vector3.forward)
                            : Quaternion.AngleAxis(inst.rotationRad * Mathf.Rad2Deg, Vector3.up);

                        Vector3 scl = Vector3.one * inst.scale;

                        mats[i] = Matrix4x4.TRS(pos, rot, scl);

                        Color c = inst.tint;
                        tints[i] = new Vector4(c.r, c.g, c.b, c.a);
                    }

                    _matBatches.Add(mats);
                    _tintBatches.Add(tints);
                    _batchCounts.Add(n);

                    offset += n;
                }
            }

            public void Draw(
                Mesh mesh,
                Material mat,
                MaterialPropertyBlock sharedMpb,
                Camera cam,
                ShadowCastingMode shadowMode,
                bool receiveShadows)
            {
                // We reuse shared MPB but must set per-batch arrays before each call
                for (int bi = 0; bi < _matBatches.Count; bi++)
                {
                    var mats = _matBatches[bi];
                    var tints = _tintBatches[bi];
                    int count = _batchCounts[bi];

                    sharedMpb.SetVectorArray("_InstTint", tints);

                    Graphics.DrawMeshInstanced(
                        mesh, 0, mat,
                        mats, count,
                        sharedMpb,
                        shadowMode,
                        receiveShadows,
                        layer: 0,
                        camera: cam
                    );
                }
            }
        }
    }
}
