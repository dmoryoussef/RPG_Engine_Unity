using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGrid.Runtime.Chunks;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.Tiles;
using WorldGrid.Runtime.World;
using WorldGrid.Unity;
using WorldGrid.Unity.Assets;

namespace WorldGrid.Unity.Rendering
{
    /// <summary>
    /// Renders a window of chunk meshes using a single tile library/material.
    ///
    /// Responsibilities:
    /// - Resolves TileLibrary + atlas Material via provider+key or fallback asset
    /// - Creates per-chunk mesh GameObjects for the view window
    /// - Rebuilds meshes for dirty chunks within the view
    /// - Optional debug overlay indicating storage kind
    ///
    /// Ownership:
    /// - Owns chunk meshes and chunk root GameObjects it creates
    /// - Owns its runtime overlay material
    /// - Does not own provider-instanced atlas materials
    /// </summary>
    public sealed class ChunkWorldRenderer : MonoBehaviour
    {
        #region Inspector

        [Header("Refs")]
        [SerializeField] private WorldHost worldHost;

        [Tooltip("Legacy fallback tile library asset (used if no TileLibrarySource is configured).")]
        [SerializeField] private TileLibraryAsset tileLibraryAsset;

        [Header("Tile Library Source (Optional)")]
        [Tooltip("Optional MonoBehaviour implementing ITileLibrarySource (for example TileLibraryProvider).")]
        [SerializeField] private MonoBehaviour tileSourceBehaviour;

        [Tooltip("Key used with ITileLibrarySource. If empty or missing, falls back to TileLibraryAsset.")]
        [SerializeField] private TileLibraryKey tileLibraryKey;

        [Header("Rendering")]
        [Tooltip("Chunks rendered in X and Y (width / height).")]
        [SerializeField] private Vector2Int viewChunksSize = new Vector2Int(4, 4);

        [Tooltip("Chunk coordinate for the bottom-left of the view window.")]
        [SerializeField] private ChunkCoordSerializable viewChunkMin = new ChunkCoordSerializable(0, 0);

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

        private TileLibrary _tileLibrary;
        private Material _atlasMaterial;

        private Material _debugOverlayMaterial;
        private MaterialPropertyBlock _overlayMpb;

        private readonly MeshData _tilesMeshData = new();

        private bool _lastShowDebugOverlay;
        private float _lastDebugOverlayAlpha;

        private readonly Dictionary<ChunkCoord, ChunkView> _views = new();

        #endregion

        #region Properties

        public ChunkCoord ViewChunkMin => new ChunkCoord(viewChunkMin.x, viewChunkMin.y);
        public Vector2Int ViewChunksSize => viewChunksSize;

        private float CellSize => worldHost != null ? worldHost.CellSize : 1f;
        private Transform WorldRoot => worldHost != null ? worldHost.WorldRoot : transform;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (!validateRequiredRefs())
            {
                enabled = false;
                return;
            }
        }

        private void Start()
        {
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

            initializeRuntimeRendering();
            rebuildAll();
            refreshAllOverlays();

            _lastShowDebugOverlay = showDebugOverlay;
            _lastDebugOverlayAlpha = debugOverlayAlpha;
        }

        private void LateUpdate()
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

        #endregion

        #region Public Controls

        public void SetViewChunkMin(ChunkCoord min, bool pruneViewsOutsideWindow = false)
        {
            viewChunkMin = new ChunkCoordSerializable(min.X, min.Y);

            ensureViewChunkGameObjectsExist();

            if (pruneViewsOutsideWindow)
                pruneViewsOutsideCurrentWindow();

            rebuildAll();
            refreshAllOverlays();
        }

        public void SetViewWindow(ChunkCoord min, Vector2Int size, bool pruneViewsOutsideWindow = false)
        {
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
            if (worldHost == null)
            {
                UnityEngine.Debug.LogError("ChunkWorldRenderer disabled: worldHost not assigned.", this);
                return false;
            }

            return true;
        }

        private bool tryResolveWorld()
        {
            _world = worldHost.World;
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

            if (tileLibraryAsset.atlasMaterial == null)
            {
                error = "ChunkWorldRenderer disabled: tileLibraryAsset.atlasMaterial is not assigned.";
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

            _atlasMaterial = tileLibraryAsset.atlasMaterial;
            return true;
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
            _atlasMaterial = view.AtlasMaterial;

            if (_tileLibrary == null || _atlasMaterial == null)
            {
                error = $"ChunkWorldRenderer disabled: TileLibrarySource returned invalid view for key '{key}'.";
                return false;
            }

            return true;
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
            var root = new GameObject($"Chunk_{cc.X}_{cc.Y}");
            root.transform.SetParent(WorldRoot, false);
            root.transform.localPosition = new Vector3(
                cc.X * _world.ChunkSize * CellSize,
                cc.Y * _world.ChunkSize * CellSize,
                0f
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
            mr.sharedMaterial = _atlasMaterial;
            mr.sortingLayerName = "Tiles";
            mr.sortingOrder = 0;

            var tilesMesh = new Mesh { name = $"ChunkTiles_{cc.X}_{cc.Y}" };
            mf.sharedMesh = tilesMesh;

            return tilesMesh;
        }

        private MeshRenderer createOverlayRenderer(Transform parent, ChunkCoord cc)
        {
            var overlayGo = new GameObject("DebugOverlay");
            overlayGo.transform.SetParent(parent, false);
            overlayGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);

            var omf = overlayGo.AddComponent<MeshFilter>();
            var overlayMr = overlayGo.AddComponent<MeshRenderer>();
            overlayMr.sharedMaterial = _debugOverlayMaterial;

            var overlayMesh = new Mesh { name = $"ChunkOverlay_{cc.X}_{cc.Y}" };
            omf.sharedMesh = overlayMesh;

            buildSolidQuad(overlayMesh, _world.ChunkSize, CellSize);
            return overlayMr;
        }

        private void pruneViewsOutsideCurrentWindow()
        {
            var size = clampViewSize(viewChunksSize);

            bool inWindow(ChunkCoord cc)
            {
                return cc.X >= viewChunkMin.x && cc.X < viewChunkMin.x + size.x
                       && cc.Y >= viewChunkMin.y && cc.Y < viewChunkMin.y + size.y;
            }

            var keys = ListPool<ChunkCoord>.Get();
            try
            {
                foreach (var k in _views.Keys)
                    keys.Add(k);

                for (int i = 0; i < keys.Count; i++)
                {
                    var cc = keys[i];
                    if (inWindow(cc))
                        continue;

                    destroyView(_views[cc]);
                    _views.Remove(cc);
                }
            }
            finally
            {
                ListPool<ChunkCoord>.Release(keys);
            }
        }

        private void destroyView(ChunkView view)
        {
            if (view == null)
                return;

            if (view.TilesMesh != null)
                Destroy(view.TilesMesh);

            if (view.OverlayRenderer != null)
            {
                var mf = view.OverlayRenderer.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    Destroy(mf.sharedMesh);
            }

            if (view.Root != null)
                Destroy(view.Root);
        }

        private static Vector2Int clampViewSize(Vector2Int size)
        {
            return new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        }

        #endregion

        #region Rebuild / Dirty Processing

        private void rebuildAll()
        {
            foreach (var cc in _views.Keys)
                rebuildChunkTiles(cc);
        }

        private void rebuildChunkTiles(ChunkCoord cc)
        {
            if (!_views.TryGetValue(cc, out var view))
                return;

            _world.TryGetChunk(cc, out var chunk);

            ChunkMeshBuilder.BuildChunkTilesMesh(
                _tilesMeshData,
                view.TilesMesh,
                cc,
                chunkOrNull: chunk,
                chunkSize: _world.ChunkSize,
                defaultTileId: _world.DefaultTileId,
                tileLibrary: _tileLibrary,
                cellSize: CellSize
            );

            rebuildChunkOverlay(cc);
        }

        private void processDirtyChunks()
        {
            var dirty = ListPool<ChunkCoord>.Get();
            try
            {
                foreach (var cc in _world.DirtyRenderChunks)
                    dirty.Add(cc);

                if (dirty.Count == 0)
                    return;

                for (int i = 0; i < dirty.Count; i++)
                {
                    var cc = dirty[i];

                    if (isInView(cc))
                        rebuildChunkTiles(cc);

                    // Clear once observed so dirty set does not grow stale.
                    _world.ClearDirtyRender(cc);
                }
            }
            finally
            {
                ListPool<ChunkCoord>.Release(dirty);
            }
        }

        private bool isInView(ChunkCoord cc)
        {
            var size = clampViewSize(viewChunksSize);

            return cc.X >= viewChunkMin.x && cc.Y >= viewChunkMin.y
                   && cc.X < viewChunkMin.x + size.x && cc.Y < viewChunkMin.y + size.y;
        }

        #endregion

        #region Overlay

        private bool didOverlaySettingsChange()
        {
            return _lastShowDebugOverlay != showDebugOverlay
                   || Mathf.Abs(_lastDebugOverlayAlpha - debugOverlayAlpha) > 0.0001f;
        }

        private void refreshAllOverlays()
        {
            foreach (var cc in _views.Keys)
                rebuildChunkOverlay(cc);
        }

        private void rebuildChunkOverlay(ChunkCoord cc)
        {
            if (!_views.TryGetValue(cc, out var view))
                return;

            if (view.OverlayRenderer == null)
                return;

            view.OverlayRenderer.enabled = showDebugOverlay;
            if (!showDebugOverlay)
                return;

            var c = getOverlayColor(cc);
            c.a = Mathf.Clamp01(debugOverlayAlpha);

            _overlayMpb.Clear();
            view.OverlayRenderer.GetPropertyBlock(_overlayMpb);
            _overlayMpb.SetColor(ColorPropId, c);
            view.OverlayRenderer.SetPropertyBlock(_overlayMpb);
        }

        private Color getOverlayColor(ChunkCoord cc)
        {
            bool hasChunk = _world.TryGetChunk(cc, out var chunk);

            if (!hasChunk || chunk == null)
                return debugOverlayMissing;

            return chunk.StorageKind == ChunkStorageKind.Uniform
                ? debugOverlayUniform
                : debugOverlayDense;
        }

        private static void buildSolidQuad(Mesh mesh, int chunkSize, float cellSize)
        {
            float w = chunkSize * cellSize;
            float h = chunkSize * cellSize;

            var verts = new Vector3[4]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(w, 0f, 0f),
                new Vector3(w, h, 0f),
                new Vector3(0f, h, 0f)
            };

            var tris = new int[6]
            {
                0, 2, 1,
                0, 3, 2
            };

            mesh.Clear();
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
        }

        #endregion

        #region Cleanup

        private void cleanupRuntime()
        {
            // Destroy renderer-owned overlay material.
            if (_debugOverlayMaterial != null)
            {
                Destroy(_debugOverlayMaterial);
                _debugOverlayMaterial = null;
            }

            // Destroy renderer-owned meshes and roots.
            foreach (var kvp in _views)
                destroyView(kvp.Value);

            _views.Clear();
        }

        #endregion

        #region Nested Types

        private sealed class ChunkView
        {
            public GameObject Root { get; }
            public Mesh TilesMesh { get; }
            public MeshRenderer OverlayRenderer { get; }

            public ChunkView(GameObject root, Mesh tilesMesh, MeshRenderer overlayRenderer)
            {
                Root = root;
                TilesMesh = tilesMesh;
                OverlayRenderer = overlayRenderer;
            }
        }

        [Serializable]
        private struct ChunkCoordSerializable
        {
            public int x;
            public int y;

            public ChunkCoordSerializable(int x, int y)
            {
                this.x = x;
                this.y = y;
            }
        }

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> Pool = new();

            public static List<T> Get()
            {
                if (Pool.Count > 0)
                    return Pool.Pop();

                return new List<T>(64);
            }

            public static void Release(List<T> list)
            {
                list.Clear();
                Pool.Push(list);
            }
        }

        #endregion
    }
}
