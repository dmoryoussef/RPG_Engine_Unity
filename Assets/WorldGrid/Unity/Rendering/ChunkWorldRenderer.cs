using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGrid.Runtime.Chunks;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.Tiles;
using WorldGrid.Runtime.World;
using WorldGrid.Unity;
using WorldGrid.Unity.Assets;

using WorldGrid.Runtime.Rendering;
namespace WorldGrid.Unity.Rendering
{
    /// <summary>
    /// Renders a window of chunk meshes using a single tile library/material.
    ///
    /// Responsibilities:
    /// - Resolves TileLibrary + atlas texture via provider+key or fallback asset
    /// - Creates per-chunk mesh GameObjects for the view window
    /// - Rebuilds meshes for dirty chunks within the view
    /// - Optional debug overlay indicating storage kind (Uniform/Dense) and missing-ness
    ///
    /// Ownership:
    /// - Owns chunk meshes and chunk root GameObjects it creates
    /// - Owns its runtime overlay material
    /// - Renderer owns the tile render material (tileMaterial). TileLibraryAsset does not need to store materials/shaders.
    /// </summary>
    public sealed class ChunkWorldRenderer : MonoBehaviour, IRenderChunkChannel
    {
        #region Inspector

        [Header("Refs")]
        [SerializeField] private WorldHost _worldHost;

        [Tooltip("Legacy fallback tile library asset (used if no TileLibrarySource is configured).")]
        [SerializeField] private TileLibraryAsset tileLibraryAsset;

        [Header("Tile Library Source (Optional)")]
        [Tooltip("Optional MonoBehaviour implementing ITileLibrarySource (for example TileLibraryProvider).")]
        [SerializeField] private MonoBehaviour tileSourceBehaviour;

        [Tooltip("Key used with ITileLibrarySource. If empty or missing, falls back to TileLibraryAsset.")]
        [SerializeField] private TileLibraryKey tileLibraryKey;

        [Tooltip("Material used to render tile meshes. The renderer owns this material; TileLibraryAsset does not.")]
        [SerializeField] private Material tileMaterial;


        [Tooltip("World-space Z used for this renderer's layer. Layering is owned by renderers, not materials.")]
        [SerializeField] private float renderLayerZ = WorldRenderZ.TilesGround;

        [Header("Debug (Read Only)")]
        [SerializeField] private string debugBoundTileLibrary;
        [Tooltip("If true, the renderer will assign the resolved atlas texture to tileMaterial (_MainTex/_BaseMap) when possible.")]
        [SerializeField] private bool autoAssignAtlasTextureToMaterial = true;

        [Header("Rendering")]
        [Tooltip("Chunks rendered in X and Y (width / height).")]
        [SerializeField] private Vector2Int viewChunksSize = new Vector2Int(4, 4);

        [Tooltip("Chunk coordinate for the bottom-left of the view window.")]
        [SerializeField] private Vector2Int viewChunkMin = Vector2Int.zero;

        [Header("Relief (Optional)")]
        [SerializeField] private bool applyReliefParams = true;
        [Header("Material Adoption (Optional)")]
        [SerializeField] private bool autoAdoptProviderShader = true;
        [SerializeField] private bool autoCopyProviderReliefParams = true;

        [SerializeField, Range(0f, 10f)] private float reliefStrength = 3.5f;
        [SerializeField, Range(0f, 1f)] private float reliefAmbient = 0.65f;
        [SerializeField] private Vector3 fakeLightDir = new Vector3(0.35f, 0.9f, 0.25f);

        [SerializeField, Range(0.1f, 50f)] private float noiseScale = 6.0f;
        [SerializeField, Range(1f, 6f)] private float noiseOctaves = 3f;
        [SerializeField, Range(0.1f, 0.9f)] private float noisePersistence = 0.5f;
        [SerializeField, Range(1.2f, 4f)] private float noiseLacunarity = 2.0f;

        [SerializeField, Range(-1f, 1f)] private float heightBias = 0.0f;
        [SerializeField, Range(0.1f, 3f)] private float heightContrast = 1.0f;

        [Header("Relief - Curvature AO")]
        [SerializeField, Range(0f, 1f)] private float curvatureAoStrength = 0.45f;
        [SerializeField, Range(0.1f, 10f)] private float curvatureAoGain = 2.5f;

        [Header("Relief - Tile Edge AO")]
        [SerializeField, Range(0f, 1f)] private float tileEdgeAoStrength = 0.30f;
        [SerializeField, Range(0.01f, 0.5f)] private float tileEdgeWidth = 0.07f;
        [SerializeField, Range(0.1f, 5f)] private float tileSize = 1.0f;

        [Header("Debug Overlay")]
        [SerializeField] private bool showDebugOverlay = true;

        [Range(0f, 1f)]
        [SerializeField] private float debugOverlayAlpha = 0.20f;

        [SerializeField] private Color debugOverlayMissing = new Color(1f, 0f, 0f, 1f);
        [SerializeField] private Color debugOverlayUniform = new Color(0f, 1f, 0f, 1f);
        [SerializeField] private Color debugOverlayDense = new Color(1f, 1f, 0f, 1f);

        #endregion

        #region Constants

        private static readonly int ColorPropId = Shader.PropertyToID("_Color");

        #endregion

        #region State

        private SparseChunkWorld _world;

        // --- Render channel bindings ---
        private SparseChunkWorld _boundWorld;
        private ITileLibraryView _boundTiles;
        private IChunkViewWindowProvider _boundView;
        private bool _boundViaOrchestrator;


        private TileLibrary _tileLibrary;
        private Texture _resolvedAtlasTexture;

        private Material _debugOverlayMaterial;
        private MaterialPropertyBlock _overlayMpb;

        private readonly MeshData _tilesMeshData = new();

        private bool _lastShowDebugOverlay;
        private float _lastDebugOverlayAlpha;

        private readonly Dictionary<ChunkCoord, ChunkView> _views = new();
        private GameObject _chunkRootsParent;

        #endregion

        #region Properties

        public ChunkCoord ViewChunkMin => (_boundView != null ? _boundView.ViewMin : new ChunkCoord(viewChunkMin.x, viewChunkMin.y));
        public Vector2Int ViewChunksSize => (_boundView != null ? _boundView.ViewSize : viewChunksSize);
        private float CellSize => _worldHost != null ? _worldHost.CellSize : 1f;
        private Transform WorldRoot => _worldHost != null ? _worldHost.WorldRoot : transform;

        #endregion

        private void updateDebugReadout()
        {
            debugBoundTileLibrary = _boundTiles != null ? _boundTiles.Key.ToString() : "<unbound>";
        }

        #region Unity Lifecycle

        private void Awake()
        {
            _chunkRootsParent = new GameObject("ChunkViewsRoot");
            _chunkRootsParent.transform.SetParent(transform, false);

            if (!validateRequiredRefs())
            {
                enabled = false;
                return;
            }
        }


        private void Start()
        {
            if (_boundViaOrchestrator)
            {
                // Bound via orchestrator; do not self-resolve.
                return;
            }

            if (!tryResolveWorld())
            {
                enabled = false;
                return;
            }

            if (!tryResolveTileResources(out var error))
            {
                UnityEngine.Debug.LogError(error, this);
                enabled = false;
                return;
            }

            ensureTileMaterial();
            if (!enabled) return;

            initializeRuntimeRendering();
            rebuildAll();
            refreshAllOverlays();


            updateDebugReadout();
            _lastShowDebugOverlay = showDebugOverlay;
            _lastDebugOverlayAlpha = debugOverlayAlpha;
        }



        private void LateUpdate()
        {
            if (_boundViaOrchestrator)
                return;

            TickInternal();
        }

        private void TickInternal()
        {
            if (_world == null)
                return;

            if (didOverlaySettingsChange())
            {
                refreshAllOverlays();
                _lastShowDebugOverlay = showDebugOverlay;
                _lastDebugOverlayAlpha = debugOverlayAlpha;
            }

            processDirtyChunks();
        }

        private void OnDisable()
        {
            cleanupRuntime();
        }

        private void OnDestroy()
        {
            cleanupRuntime();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                return;

            // Make sure tileMaterial exists (your ensure handles null)
            ensureTileMaterial();

            // IMPORTANT: if you enabled "Auto Copy Provider Relief Params",
            // make sure it does NOT run repeatedly (Bind only).
            tryAssignReliefParamsToMaterial(); // the method that does tileMaterial.SetFloat(...)
        }


        #endregion

        #region Public Controls

        public void SetViewChunkMin(ChunkCoord min, bool pruneViewsOutsideWindow = false)
        {
            if (!enabled || _world == null || _chunkRootsParent == null)
                return;

            viewChunkMin = new Vector2Int(min.X, min.Y);

            ensureViewChunkGameObjectsExist();

            if (pruneViewsOutsideWindow)
                pruneViewsOutsideCurrentWindow();

            rebuildAll();
            refreshAllOverlays();
        }

        public void SetViewWindow(ChunkCoord min, Vector2Int size, bool pruneViewsOutsideWindow = false)
        {
            if (!enabled || _world == null || _chunkRootsParent == null)
                return;
            viewChunksSize = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            SetViewChunkMin(min, pruneViewsOutsideWindow);
        }

        public void NudgeViewChunkMin(int dx, int dy, bool pruneViewsOutsideWindow = false)
        {
            var cur = ViewChunkMin;
            SetViewChunkMin(new ChunkCoord(cur.X + dx, cur.Y + dy), pruneViewsOutsideWindow);
        }

        #endregion

        #region Validation / Resolution

        private bool validateRequiredRefs()
        {
            //if (_worldHost == null)
            //{
            //    UnityEngine.Debug.LogError("ChunkWorldRenderer disabled: worldHost not assigned.", this);
            //    return false;
            //}

            // tileMaterial is allowed to be null; we'll auto-create one at runtime.
            return true;
        }
        private void tryAssignReliefParamsToMaterial()
        {
            if (!applyReliefParams || tileMaterial == null) return;

            if (tileMaterial.HasProperty("_ReliefStrength")) tileMaterial.SetFloat("_ReliefStrength", reliefStrength);
            if (tileMaterial.HasProperty("_Ambient")) tileMaterial.SetFloat("_Ambient", reliefAmbient);
            if (tileMaterial.HasProperty("_FakeLightDir")) tileMaterial.SetVector("_FakeLightDir", new Vector4(fakeLightDir.x, fakeLightDir.y, fakeLightDir.z, 0));

            if (tileMaterial.HasProperty("_NoiseScale")) tileMaterial.SetFloat("_NoiseScale", noiseScale);
            if (tileMaterial.HasProperty("_NoiseOctaves")) tileMaterial.SetFloat("_NoiseOctaves", noiseOctaves);
            if (tileMaterial.HasProperty("_NoisePersistence")) tileMaterial.SetFloat("_NoisePersistence", noisePersistence);
            if (tileMaterial.HasProperty("_NoiseLacunarity")) tileMaterial.SetFloat("_NoiseLacunarity", noiseLacunarity);

            if (tileMaterial.HasProperty("_HeightBias")) tileMaterial.SetFloat("_HeightBias", heightBias);
            if (tileMaterial.HasProperty("_HeightContrast")) tileMaterial.SetFloat("_HeightContrast", heightContrast);

            if (tileMaterial.HasProperty("_CurvatureAO"))
                tileMaterial.SetFloat("_CurvatureAO", curvatureAoStrength);

            if (tileMaterial.HasProperty("_CurvatureAOGain"))
                tileMaterial.SetFloat("_CurvatureAOGain", curvatureAoGain);

            if (tileMaterial.HasProperty("_TileEdgeAO"))
                tileMaterial.SetFloat("_TileEdgeAO", tileEdgeAoStrength);

            if (tileMaterial.HasProperty("_TileEdgeWidth"))
                tileMaterial.SetFloat("_TileEdgeWidth", tileEdgeWidth);

            if (tileMaterial.HasProperty("_TileSize"))
                tileMaterial.SetFloat("_TileSize", tileSize);

        }

        private void ensureTileMaterial()
        {
            if (tileMaterial != null)
                return;

            // Pick a safe fallback shader
            var shader =
                Shader.Find("WorldGrid/Unlit Vertex Tint Blend")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Unlit/Texture");

            if (shader == null)
            {
                UnityEngine.Debug.LogError("ChunkWorldRenderer disabled: no fallback shader found to auto-create tileMaterial.", this);
                enabled = false;
                return;
            }

            tileMaterial = new Material(shader)
            {
                name = "WorldGrid_RuntimeTileMaterial"
            };

            UnityEngine.Debug.LogWarning("ChunkWorldRenderer: tileMaterial was null, auto-created runtime material.", this);

            // If we already resolved an atlas texture, bind it now.
            tryAssignAtlasTextureToMaterial();
        }

        private bool tryResolveWorld()
        {
            _world = _worldHost.World;
            if (_world == null)
            {
                UnityEngine.Debug.LogError("ChunkWorldRenderer disabled: worldHost.World is null.", this);
                return false;
            }

            return true;
        }

        private bool tryResolveTileResources(out string error)
        {
            // Prefer provider+key if configured and available.
            if (tryResolveFromProvider(out error))
                return true;

            // Fallback to direct asset.
            return tryResolveFromFallbackAsset(out error);
        }

        private bool tryResolveFromProvider(out string error)
        {
            error = null;

            var source = tileSourceBehaviour as ITileLibrarySource;
            if (source == null)
                return false;

            if (tileLibraryKey.IsEmpty)
                return false;

            if (!source.Has(tileLibraryKey))
            {
                error = $"ChunkWorldRenderer disabled: TileLibrarySource has no entry for key '{tileLibraryKey}'.";
                return false;
            }

            // Use provider's runtime-safe path when available.
            if (source is TileLibraryProvider provider)
            {
                if (!provider.TryGet(tileLibraryKey, out var view, out var providerError))
                {
                    error = $"ChunkWorldRenderer disabled: {providerError}";
                    return false;
                }

                return tryApplyView(view, tileLibraryKey, out error);
            }

            // Interface fallback: call Get only after Has; still validate returned view.
            ITileLibraryView v;
            try
            {
                v = source.Get(tileLibraryKey);
            }
            catch (Exception ex)
            {
                error = $"ChunkWorldRenderer disabled: TileLibrarySource threw while resolving key '{tileLibraryKey}': {ex.Message}";
                return false;
            }

            return tryApplyView(v, tileLibraryKey, out error);
        }

        private bool tryResolveFromFallbackAsset(out string error)
        {
            error = null;

            if (tileLibraryAsset == null)
            {
                error = "ChunkWorldRenderer disabled: No TileLibrarySource configured and tileLibraryAsset is null.";
                return false;
            }

            try
            {
                _tileLibrary = tileLibraryAsset.BuildRuntime();
            }
            catch (Exception ex)
            {
                error = $"ChunkWorldRenderer disabled: Failed to BuildRuntime() for fallback asset: {ex.Message}";
                return false;
            }

            _resolvedAtlasTexture = tileLibraryAsset.atlasTexture;
            tryAssignAtlasTextureToMaterial();
            return true;
        }

        private static void copyIfFloat(Material src, Material dst, string prop)
        {
            if (src == null || dst == null) return;
            if (!src.HasProperty(prop) || !dst.HasProperty(prop)) return;
            dst.SetFloat(prop, src.GetFloat(prop));
        }

        private static void copyIfVector(Material src, Material dst, string prop)
        {
            if (src == null || dst == null) return;
            if (!src.HasProperty(prop) || !dst.HasProperty(prop)) return;
            dst.SetVector(prop, src.GetVector(prop));
        }

        private static void CopyFloatIfPresent(Material src, Material dst, string prop)
        {
            if (src == null || dst == null) return;
            if (!src.HasProperty(prop) || !dst.HasProperty(prop)) return;
            dst.SetFloat(prop, src.GetFloat(prop));
        }

        private static void CopyVectorIfPresent(Material src, Material dst, string prop)
        {
            if (src == null || dst == null) return;
            if (!src.HasProperty(prop) || !dst.HasProperty(prop)) return;
            dst.SetVector(prop, src.GetVector(prop));
        }

        private void tryAdoptProviderMaterial(Material providerMat)
        {
            if (tileMaterial == null || providerMat == null)
                return;

            // Adopt shader so the renderer actually runs the relief shader
            if (tileMaterial.shader != providerMat.shader)
                tileMaterial.shader = providerMat.shader;

            // Copy key relief/noise params (safe: only if both have the property)
            CopyFloatIfPresent(providerMat, tileMaterial, "_ReliefStrength");
            CopyFloatIfPresent(providerMat, tileMaterial, "_Ambient");
            CopyVectorIfPresent(providerMat, tileMaterial, "_FakeLightDir");

            CopyFloatIfPresent(providerMat, tileMaterial, "_NoiseScale");
            CopyFloatIfPresent(providerMat, tileMaterial, "_NoiseOctaves");
            CopyFloatIfPresent(providerMat, tileMaterial, "_NoisePersistence");
            CopyFloatIfPresent(providerMat, tileMaterial, "_NoiseLacunarity");

            CopyFloatIfPresent(providerMat, tileMaterial, "_HeightBias");
            CopyFloatIfPresent(providerMat, tileMaterial, "_HeightContrast");
        }

        private bool tryApplyView(ITileLibraryView view, TileLibraryKey key, out string error)
        {
            error = null;

            if (view == null)
            {
                error = $"ChunkWorldRenderer disabled: TileLibrarySource returned null view for key '{key}'.";
                return false;
            }

            _tileLibrary = view.Library;

            var viewMat = view.AtlasMaterial;

            // Keep your existing texture copy behavior
            _resolvedAtlasTexture = viewMat != null ? viewMat.mainTexture : null;
            tryAssignAtlasTextureToMaterial();

            // NEW: adopt provider shader (so relief actually runs)
            if (autoAdoptProviderShader && tileMaterial != null && viewMat != null)
            {
                if (tileMaterial.shader != viewMat.shader)
                    tileMaterial.shader = viewMat.shader;
            }

            // copy relief params (so template defaults carry over)
            if (autoCopyProviderReliefParams && tileMaterial != null && viewMat != null)
            {
                copyIfFloat(viewMat, tileMaterial, "_ReliefStrength");
                copyIfFloat(viewMat, tileMaterial, "_Ambient");
                copyIfVector(viewMat, tileMaterial, "_FakeLightDir");

                copyIfFloat(viewMat, tileMaterial, "_NoiseScale");
                copyIfFloat(viewMat, tileMaterial, "_NoiseOctaves");
                copyIfFloat(viewMat, tileMaterial, "_NoisePersistence");
                copyIfFloat(viewMat, tileMaterial, "_NoiseLacunarity");

                copyIfFloat(viewMat, tileMaterial, "_HeightBias");
                copyIfFloat(viewMat, tileMaterial, "_HeightContrast");
            }

            if (_tileLibrary == null)
            {
                error = $"ChunkWorldRenderer disabled: TileLibrarySource returned invalid library for key '{key}'.";
                return false;
            }

            return true;
        }

        private void tryAssignAtlasTextureToMaterial()
        {
            if (!autoAssignAtlasTextureToMaterial)
                return;

            if (tileMaterial == null)
                return;

            if (_resolvedAtlasTexture == null)
                return;

            if (tileMaterial.HasProperty("_BaseMap"))
                tileMaterial.SetTexture("_BaseMap", _resolvedAtlasTexture);

            if (tileMaterial.HasProperty("_MainTex"))
                tileMaterial.SetTexture("_MainTex", _resolvedAtlasTexture);
        }

        #endregion

        #region Initialization

        private void initializeRuntimeRendering()
        {
            _debugOverlayMaterial = createRuntimeOverlayMaterial();
            _overlayMpb = new MaterialPropertyBlock();

            ensureViewChunkGameObjectsExist();
        }

        private static Material createRuntimeOverlayMaterial()
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            if (shader == null)
            {
                UnityEngine.Debug.LogError("ChunkWorldRenderer: No suitable shader found for DebugOverlay.");
                return null;
            }

            return new Material(shader)
            {
                name = "WorldGrid_DebugOverlay_RuntimeMat"
            };
        }

        #endregion

        #region View Window / Objects

        private void ensureViewChunkGameObjectsExist()
        {
            var size = clampViewSize(viewChunksSize);

            for (int dy = 0; dy < size.y; dy++)
            {
                for (int dx = 0; dx < size.x; dx++)
                {
                    var cc = new ChunkCoord(viewChunkMin.x + dx, viewChunkMin.y + dy);
                    if (_views.ContainsKey(cc))
                        continue;

                    var view = createChunkView(cc);
                    _views.Add(cc, view);
                }
            }
        }

        private ChunkView createChunkView(ChunkCoord cc)
        {
            var root = createChunkRoot(cc);
            var tilesMesh = createTilesRenderer(root.transform, cc);

            MeshRenderer overlayMr = null;
            if (_debugOverlayMaterial != null)
                overlayMr = createOverlayRenderer(root.transform, cc);

            return new ChunkView(root, tilesMesh, overlayMr);
        }

        private GameObject createChunkRoot(ChunkCoord cc)
        {
            // Determine storage kind for naming (debug only)
            ChunkStorageKind storageKind = ChunkStorageKind.Uniform;

            if (_world.TryGetChunk(cc, out var chunk))
            {
                storageKind = chunk.StorageKind;
            }

            var root = new GameObject(
                $"Chunk_{cc.X}_{cc.Y}_{storageKind}"
            );

            root.transform.SetParent(_chunkRootsParent.transform, false);
            root.transform.localPosition = new Vector3(
                cc.X * _world.ChunkSize * CellSize,
                cc.Y * _world.ChunkSize * CellSize,
                renderLayerZ
            );

            return root;
        }


        private Mesh createTilesRenderer(Transform parent, ChunkCoord cc)
        {
            var tilesGo = new GameObject("Tiles");
            tilesGo.transform.SetParent(parent, false);
            tilesGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);

            var mf = tilesGo.AddComponent<MeshFilter>();
            var mr = tilesGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = tileMaterial;
            mr.sortingLayerName = "Tiles";
            mr.sortingOrder = 0;

            var tilesMesh = new Mesh { name = $"ChunkTiles_{cc.X}_{cc.Y}" };
            mf.sharedMesh = tilesMesh;

            return tilesMesh;
        }

        private MeshRenderer createOverlayRenderer(Transform parent, ChunkCoord cc)
        {
            var overlayGo = new GameObject("Overlay");
            overlayGo.transform.SetParent(parent, false);
            overlayGo.transform.localPosition = Vector3.zero;

            var mf = overlayGo.AddComponent<MeshFilter>();
            var mr = overlayGo.AddComponent<MeshRenderer>();

            mr.sharedMaterial = _debugOverlayMaterial;
            mr.sortingLayerName = "Tiles";
            mr.sortingOrder = 1;

            var overlayMesh = new Mesh { name = $"ChunkOverlay_{cc.X}_{cc.Y}" };
            mf.sharedMesh = overlayMesh;

            // overlay mesh is a full chunk quad
            buildOverlayMesh(overlayMesh, _world.ChunkSize, CellSize);

            return mr;
        }

        private static void buildOverlayMesh(Mesh overlayMesh, int chunkSize, float cellSize)
        {
            overlayMesh.Clear();

            float w = chunkSize * cellSize;
            float h = chunkSize * cellSize;

            var verts = new List<Vector3>(4)
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(w, 0f, 0f),
                new Vector3(w, h, 0f),
                new Vector3(0f, h, 0f),
            };

            var uvs = new List<Vector2>(4)
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };

            var tris = new List<int>(6) { 0, 2, 1, 0, 3, 2 };

            overlayMesh.SetVertices(verts);
            overlayMesh.SetUVs(0, uvs);
            overlayMesh.SetTriangles(tris, 0);

            overlayMesh.RecalculateBounds();
            overlayMesh.RecalculateNormals();
        }

        private static Vector2Int clampViewSize(Vector2Int size)
        {
            return new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        }

        private void pruneViewsOutsideCurrentWindow()
        {
            var min = ViewChunkMin;
            var size = clampViewSize(viewChunksSize);
            var max = new ChunkCoord(min.X + size.x - 1, min.Y + size.y - 1);

            var toRemove = ListPool<ChunkCoord>.Get();

            foreach (var kvp in _views)
            {
                var cc = kvp.Key;

                if (cc.X < min.X || cc.Y < min.Y || cc.X > max.X || cc.Y > max.Y)
                    toRemove.Add(cc);
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                var cc = toRemove[i];

                if (_views.TryGetValue(cc, out var view))
                {
                    view.Destroy();
                    _views.Remove(cc);
                }
            }

            ListPool<ChunkCoord>.Release(toRemove);
        }

        #endregion

        #region Rebuild / Dirty Processing

        private void rebuildAll()
        {
            ensureViewChunkGameObjectsExist();

            foreach (var kvp in _views)
            {
                rebuildChunk(kvp.Key, kvp.Value);
            }
        }
        private readonly List<ChunkCoord> _dirtyScratch = new List<ChunkCoord>(256);

        private void processDirtyChunks()
        {
            var dirty = _world.DirtyRenderChunks;
            if (dirty == null)
                return;

            // Snapshot to avoid modifying the HashSet while enumerating it.
            _dirtyScratch.Clear();
            foreach (var cc in dirty)
                _dirtyScratch.Add(cc);

            if (_dirtyScratch.Count == 0)
                return;

            var min = ViewChunkMin;
            var size = clampViewSize(viewChunksSize);
            var max = new ChunkCoord(min.X + size.x - 1, min.Y + size.y - 1);

            for (int i = 0; i < _dirtyScratch.Count; i++)
            {
                var cc = _dirtyScratch[i];

                // Only rebuild chunks inside the view window
                if (cc.X >= min.X && cc.Y >= min.Y && cc.X <= max.X && cc.Y <= max.Y)
                {
                    if (_views.TryGetValue(cc, out var view))
                        rebuildChunk(cc, view);
                }

                // Clear dirty flag AFTER rebuild
                _world.ClearDirtyRender(cc);
            }

            _dirtyScratch.Clear();
        }


        private void rebuildChunk(ChunkCoord cc, ChunkView view)
        {
            if (view == null)
                return;

            // Determine whether chunk exists; missing-ness is not part of ChunkStorageKind.
            bool hasChunk = _world.TryGetChunk(cc, out var chunk);
            view.HasChunk = hasChunk && chunk != null;
            view.StorageKind = view.HasChunk ? chunk.StorageKind : default;



            // Keep debug overlay in sync even when no global refresh is triggered.
            refreshOverlay(view);
            // Build tiles mesh for this chunk.
            var mf = view.Root.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                return;

            Mesh tilesMesh = mf.sharedMesh;

            ChunkMeshBuilder.BuildChunkTilesMesh(
                md: _tilesMeshData,
                mesh: tilesMesh,
                chunkCoord: cc,
                chunkOrNull: chunk,
                chunkSize: _world.ChunkSize,
                defaultTileId: _world.DefaultTileId,
                tileLibrary: _tileLibrary,
                cellSize: CellSize,
                layerZ: renderLayerZ
            );
        }

        #endregion

        #region Debug Overlay

        private bool didOverlaySettingsChange()
        {
            if (_lastShowDebugOverlay != showDebugOverlay)
                return true;

            if (!Mathf.Approximately(_lastDebugOverlayAlpha, debugOverlayAlpha))
                return true;

            return false;
        }

        private void refreshAllOverlays()
        {
            foreach (var kvp in _views)
            {
                refreshOverlay(kvp.Value);
            }
        }

        private void refreshOverlay(ChunkView view)
        {
            if (view == null)
                return;

            if (view.OverlayMr == null)
                return;

            view.OverlayMr.enabled = showDebugOverlay;

            if (!showDebugOverlay)
                return;

            Color baseColor = !view.HasChunk
                ? debugOverlayMissing
                : view.StorageKind switch
                {
                    ChunkStorageKind.Uniform => debugOverlayUniform,
                    ChunkStorageKind.Dense => debugOverlayDense,
                    _ => debugOverlayMissing
                };

            baseColor.a *= Mathf.Clamp01(debugOverlayAlpha);

            _overlayMpb.Clear();
            _overlayMpb.SetColor(ColorPropId, baseColor);
            view.OverlayMr.SetPropertyBlock(_overlayMpb);
        }

        #endregion

        #region Cleanup

        private void cleanupRuntime()
        {
            if (_debugOverlayMaterial != null)
            {
                Destroy(_debugOverlayMaterial);
                _debugOverlayMaterial = null;
            }

            if (_chunkRootsParent != null)
            {
                Destroy(_chunkRootsParent);
                _chunkRootsParent = null;
            }

            _views.Clear();
        }

        #endregion

        #region View Type

        private sealed class ChunkView
        {
            public GameObject Root { get; }
            public Mesh TilesMesh { get; }
            public MeshRenderer OverlayMr { get; }

            public bool HasChunk { get; set; }
            public ChunkStorageKind StorageKind { get; set; }

            public ChunkView(GameObject root, Mesh tilesMesh, MeshRenderer overlayMr)
            {
                Root = root;
                TilesMesh = tilesMesh;
                OverlayMr = overlayMr;
            }

            public void Destroy()
            {
                if (Root != null)
                {
                    UnityEngine.Object.Destroy(Root);
                }
            }
        }

        #endregion

        #region List Pool

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> Pool = new Stack<List<T>>();

            public static List<T> Get()
            {
                if (Pool.Count > 0)
                    return Pool.Pop();

                return new List<T>();
            }

            public static void Release(List<T> list)
            {
                list.Clear();
                Pool.Push(list);
            }
        }

        #endregion


        #region IRenderChunkChannel

        public void Bind(WorldHost worldHost, SparseChunkWorld world, ITileLibraryView tiles, IChunkViewWindowProvider view)
        {
            _boundViaOrchestrator = true;

            _boundWorld = world;
            _boundTiles = tiles;
            _boundView = view;
            _worldHost = worldHost;
            _world = world;

            // Apply tile library view (sets _tileLibrary and binds atlas texture into tileMaterial when configured).
            if (!tryApplyView(tiles, tiles != null ? tiles.Key : default, out var error))
            {
                UnityEngine.Debug.LogError(error, this);
                enabled = false;
                return;
            }

            // Ensure a material exists (will NOT override the provider view's atlas assignment).
            ensureTileMaterial();

            var providerMat = tiles != null ? tiles.AtlasMaterial : null;
            tryAdoptProviderMaterial(providerMat);

            UnityEngine.Debug.Log($"[ChunkWorldRenderer:{name}] Adopted provider shader -> tileShader='{tileMaterial.shader.name}', providerShader='{providerMat?.shader.name}'");
            if (tileMaterial.HasProperty("_ReliefStrength"))
                UnityEngine.Debug.Log($"[ChunkWorldRenderer:{name}] Relief now: _ReliefStrength={tileMaterial.GetFloat("_ReliefStrength")} _NoiseScale={tileMaterial.GetFloat("_NoiseScale")}");

            tryAssignAtlasTextureToMaterial();
            if (!enabled)
                return;

            // Initialize runtime objects if needed.
            initializeRuntimeRendering();

            // Subscribe to world events that affect overlays and chunk view lifecycle.
            if (_world != null)
            {
                _world.ChunkCreated += OnWorldChunkCreated;
                _world.ChunkRemoved += OnWorldChunkRemoved;
                _world.ChunkStorageKindChanged += OnWorldChunkStorageKindChanged;
            }

            if (_boundView != null)
            {
                _boundView.ViewChanged += OnViewChanged;

                // Apply current view immediately (and prune out-of-window chunk GOs).
                SetViewWindow(_boundView.ViewMin, _boundView.ViewSize, pruneViewsOutsideWindow: true);
            }

            updateDebugReadout();


            // Full rebuild on bind to avoid "stuck at defaults" or missed events.
            rebuildAll();
            refreshAllOverlays();

            _lastShowDebugOverlay = showDebugOverlay;
            _lastDebugOverlayAlpha = debugOverlayAlpha;
        }

        public void Unbind()
        {
            if (_boundView != null)
                _boundView.ViewChanged -= OnViewChanged;

            if (_world != null)
            {
                _world.ChunkCreated -= OnWorldChunkCreated;
                _world.ChunkRemoved -= OnWorldChunkRemoved;
                _world.ChunkStorageKindChanged -= OnWorldChunkStorageKindChanged;
            }

            _boundWorld = null;
            _boundTiles = null;
            _boundView = null;
            _boundViaOrchestrator = false;
        }

        public void Tick()
        {
            // Hard pull: if a ViewChanged event was missed, don't remain stuck on serialized defaults (e.g., 4x4).
            if (_boundViaOrchestrator && _boundView != null)
            {
                SetViewWindow(_boundView.ViewMin, _boundView.ViewSize, pruneViewsOutsideWindow: true);
            }

            TickInternal();
        }

        private void OnViewChanged(ChunkCoord min, Vector2Int size)
        {
            SetViewWindow(min, size, pruneViewsOutsideWindow: true);
        }

       // updateDebugReadout();


        private void OnWorldChunkCreated(ChunkCoord cc)
        {
            if (_views != null && _views.TryGetValue(cc, out var view))
            {
                rebuildChunk(cc, view);
            }
        }

        private void OnWorldChunkRemoved(ChunkCoord cc)
        {
            if (_views != null && _views.TryGetValue(cc, out var view))
            {
                view.Destroy();
                _views.Remove(cc);
            }
        }

        private void OnWorldChunkStorageKindChanged(ChunkCoord cc, ChunkStorageKind oldKind, ChunkStorageKind newKind)
        {
            if (_views != null && _views.TryGetValue(cc, out var view))
            {
                view.HasChunk = true;
                view.StorageKind = newKind;
                refreshOverlay(view);
            }
        }

        #endregion

    }
}