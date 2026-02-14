using Grass;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldGrid.Unity.Rendering
{
    [DisallowMultipleComponent]
    public sealed class GrassSingleProfileDrawTest : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] private GrassRenderProfile profile;

        [Header("Placement")]
        [SerializeField] private Vector3 worldPosition = new Vector3(0.5f, 0.5f, 0.1f);
        [SerializeField] private float rotationDeg = 0f;
        [SerializeField] private Vector3 scale = Vector3.one;

        [Header("Draw Mode")]
        [Tooltip("If true, uses DrawMeshInstanced with 1 instance")]
        [SerializeField] private bool useInstancedDraw = true;

        [Header("Debug")]
        [SerializeField] private bool logOnce = true;

        private Material _runtimeMat;
        private MaterialPropertyBlock _mpb;
        private bool _logged;

        // Common shader IDs
        private static readonly int ID_MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int ID_BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int ID_Color = Shader.PropertyToID("_Color");
        private static readonly int ID_BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ID_Cutoff = Shader.PropertyToID("_Cutoff");
        private static readonly int ID_AlphaCutoff = Shader.PropertyToID("_AlphaCutoff");
        private static readonly int ID_UseXZPlane = Shader.PropertyToID("_UseXZPlane");

        private void OnEnable()
        {
            _mpb ??= new MaterialPropertyBlock();

            if (profile == null)
            {
                UnityEngine.Debug.LogError("[GrassSingleProfileDrawTest] profile is null.", this);
                return;
            }

            if (!profile.IsValid(out string err))
            {
                UnityEngine.Debug.LogError($"[GrassSingleProfileDrawTest] profile invalid: {err}", this);
                return;
            }

            if (profile.materialTemplate == null)
            {
                UnityEngine.Debug.LogError("[GrassSingleProfileDrawTest] profile.materialTemplate is null.", this);
                return;
            }

            _runtimeMat = new Material(profile.materialTemplate)
            {
                name = $"{profile.materialTemplate.name} (SingleProfile Runtime)"
            };

            _runtimeMat.enableInstancing = true;

            if (profile.renderQueueOverride >= 0)
                _runtimeMat.renderQueue = profile.renderQueueOverride;

            if (profile.mainTextureOverride != null)
            {
                if (_runtimeMat.HasProperty(ID_MainTex))
                    _runtimeMat.SetTexture(ID_MainTex, profile.mainTextureOverride);
                if (_runtimeMat.HasProperty(ID_BaseMap))
                    _runtimeMat.SetTexture(ID_BaseMap, profile.mainTextureOverride);
            }

            // Force non-zero cutoff so alpha==0 pixels discard
            //float cutoff = Mathf.Max(0.001f, profile.alphaCutoff);
            //if (_runtimeMat.HasProperty(ID_Cutoff))
            //    _runtimeMat.SetFloat(ID_Cutoff, cutoff);
            //if (_runtimeMat.HasProperty(ID_AlphaCutoff))
            //    _runtimeMat.SetFloat(ID_AlphaCutoff, cutoff);

            // Set XZ/XY plane flag
            if (_runtimeMat.HasProperty(ID_UseXZPlane))
                _runtimeMat.SetFloat(ID_UseXZPlane, profile.useXZPlane);

            // Tint (grass owns its color)
            Color tint = profile.patchColorA; // or profile.defaultTint if you add one
            if (_runtimeMat.HasProperty(ID_Color))
                _mpb.SetColor(ID_Color, tint);
            if (_runtimeMat.HasProperty(ID_BaseColor))
                _mpb.SetColor(ID_BaseColor, tint);
        }

        private void OnDisable()
        {
            if (_runtimeMat != null)
            {
                Destroy(_runtimeMat);
                _runtimeMat = null;
            }
        }

        private void Update()
        {
            if (_runtimeMat == null || profile == null)
                return;

            Mesh mesh = profile.EffectiveMesh;
            if (mesh == null)
                return;

            Quaternion rot = Quaternion.Euler(0f, 0f, rotationDeg);
            Matrix4x4 m = Matrix4x4.TRS(worldPosition, rot, scale);

            if (logOnce && !_logged)
            {
                _logged = true;
                UnityEngine.Debug.Log(
                    $"[GrassSingleProfileDrawTest] draw instanced={useInstancedDraw}, " +
                    $"mesh={mesh.name}, shader={_runtimeMat.shader.name}, " +
                    $"pos={worldPosition}",
                    this);
            }

            var shadowMode = profile.castShadows
                ? ShadowCastingMode.On
                : ShadowCastingMode.Off;

            if (useInstancedDraw)
            {
                Matrix4x4[] arr = { m };
                Graphics.DrawMeshInstanced(
                    mesh, 0, _runtimeMat, arr, 1, _mpb,
                    shadowMode, profile.receiveShadows, gameObject.layer
                );
            }
            else
            {
                Graphics.DrawMesh(
                    mesh, m, _runtimeMat, gameObject.layer,
                    null, 0, _mpb,
                    shadowMode, profile.receiveShadows
                );
            }
        }
    }
}
