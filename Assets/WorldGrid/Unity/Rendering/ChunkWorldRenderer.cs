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
    public sealed class ChunkWorldRenderer : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private WorldHost worldHost;

        [Tooltip("Legacy fallback tile library asset (used if no TileLibrarySource is configured).")]
        [SerializeField] private TileLibraryAsset tileLibraryAsset;

        [Header("Tile Library Source (Optional)")]
        [Tooltip("Optional: MonoBehaviour implementing ITileLibrarySource (eg TileLibraryProvider on WorldHost).")]
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

        private static readonly int ColorPropId = Shader.PropertyToID("_Color");

        private SparseChunkWorld _world;

        // Resolved tile resources
        private TileLibrary _tileLibrary;
        private Material _atlasMaterial;

        // Runtime overlay material
        private Material _debugOverlayMaterial;

        // Mesh build scratch data
        private readonly MeshData _tilesMeshData = new();

        // Overlay state cache
        private bool _lastShowDebugOverlay;
        private float _lastDebugOverlayAlpha;

        // View storage
        private readonly Dictionary<ChunkCoord, ChunkView> _views = new();

        public ChunkCoord ViewChunkMin => new ChunkCoord(viewChunkMin.x, viewChunkMin.y);
        public Vector2Int ViewChunksSize => viewChunksSize;

        private float CellSize => worldHost != null ? worldHost.CellSize : 1f;
        private Transform WorldRoot => worldHost != null ? worldHost.WorldRoot : transform;

        private void Awake()
        {
            if (worldHost == null)
            {
                UnityEngine.Debug.LogError("ChunkWorldRenderer: worldHost not assigned.", this);
                enabled = false;
                return;
            }
        }

        private void Start()
        {
            _world = worldHost.World;
            if (_world == null)
            {
                UnityEngine.Debug.LogError("ChunkWorldRenderer: worldHost.World is null.", this);
                enabled = false;
                return;
            }

            if (!ResolveTileResources(out _tileLibrary, out _atlasMaterial, out var error))
            {
                UnityEngine.Debug.LogError(error, this);
                enabled = false;
                return;
            }

            _debugOverlayMaterial = CreateRuntimeOverlayMaterial();

            EnsureViewChunkGameObjectsExist();
            ForceRebuildAll();

            _lastShowDebugOverlay = showDebugOverlay;
            _lastDebugOverlayAlpha = debugOverlayAlpha;

            RefreshAllOverlays();
        }

        private void OnDestroy()
        {
            if (_debugOverlayMaterial != null)
            {
                Destroy(_debugOverlayMaterial);
                _debugOverlayMaterial = null;
            }
        }

        private void LateUpdate()
        {
            if (_world == null)
                return;

            if (_lastShowDebugOverlay != showDebugOverlay ||
                Mathf.Abs(_lastDebugOverlayAlpha - debugOverlayAlpha) > 0.0001f)
            {
                RefreshAllOverlays();
                _lastShowDebugOverlay = showDebugOverlay;
                _lastDebugOverlayAlpha = debugOverlayAlpha;
            }

            foreach (var cc in _world.DirtyRenderChunks)
            {
                if (!IsInView(cc))
                    continue;

                RebuildChunkTiles(cc);
            }

            ClearAllDirtyRenderFlags();
        }

        public void SetViewChunkMin(ChunkCoord min, bool pruneViewsOutsideWindow = false)
        {
            viewChunkMin = new ChunkCoordSerializable(min.X, min.Y);

            EnsureViewChunkGameObjectsExist();

            if (pruneViewsOutsideWindow)
                PruneViewsOutsideCurrentWindow();

            ForceRebuildAll();
            RefreshAllOverlays();
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

        private bool ResolveTileResources(out TileLibrary library, out Material material, out string error)
        {
            var source = tileSourceBehaviour as ITileLibrarySource;

            if (source != null && !tileLibraryKey.IsEmpty && source.Has(tileLibraryKey))
            {
                var view = source.Get(tileLibraryKey);
                library = view?.Library;
                material = view?.AtlasMaterial;

                if (library != null && material != null)
                {
                    error = null;
                    return true;
                }

                error = $"ChunkWorldRenderer: TileLibrarySource returned invalid view for key '{tileLibraryKey}'.";
                return false;
            }

            if (tileLibraryAsset == null)
            {
                library = null;
                material = null;
                error = "ChunkWorldRenderer: No TileLibrarySource configured and tileLibraryAsset is null.";
                return false;
            }

            if (tileLibraryAsset.atlasMaterial == null)
            {
                library = null;
                material = null;
                error = "ChunkWorldRenderer: tileLibraryAsset.atlasMaterial is not assigned.";
                return false;
            }

            library = tileLibraryAsset.BuildRuntime();
            material = tileLibraryAsset.atlasMaterial;
            error = null;
            return true;
        }

        private void EnsureViewChunkGameObjectsExist()
        {
            int w = Mathf.Max(1, viewChunksSize.x);
            int h = Mathf.Max(1, viewChunksSize.y);

            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                {
                    var cc = new ChunkCoord(viewChunkMin.x + dx, viewChunkMin.y + dy);
                    if (_views.ContainsKey(cc))
                        continue;

                    var root = new GameObject($"Chunk_{cc.X}_{cc.Y}");
                    root.transform.SetParent(WorldRoot, false);
                    root.transform.localPosition = new Vector3(
                        cc.X * _world.ChunkSize * CellSize,
                        cc.Y * _world.ChunkSize * CellSize,
                        0f
                    );

                    var tilesGo = new GameObject("Tiles");
                    tilesGo.transform.SetParent(root.transform, false);
                    tilesGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);

                    var mf = tilesGo.AddComponent<MeshFilter>();
                    var mr = tilesGo.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = _atlasMaterial;
                    mr.sortingLayerName = "Tiles";
                    mr.sortingOrder = 0;

                    var tilesMesh = new Mesh { name = $"ChunkTiles_{cc.X}_{cc.Y}" };
                    mf.sharedMesh = tilesMesh;

                    MeshRenderer overlayMr = null;
                    if (_debugOverlayMaterial != null)
                    {
                        var overlayGo = new GameObject("DebugOverlay");
                        overlayGo.transform.SetParent(root.transform, false);
                        overlayGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);

                        var omf = overlayGo.AddComponent<MeshFilter>();
                        overlayMr = overlayGo.AddComponent<MeshRenderer>();
                        overlayMr.sharedMaterial = _debugOverlayMaterial;

                        var overlayMesh = new Mesh { name = $"ChunkOverlay_{cc.X}_{cc.Y}" };
                        omf.sharedMesh = overlayMesh;

                        BuildSolidQuad(overlayMesh, _world.ChunkSize, CellSize);
                    }

                    _views.Add(cc, new ChunkView(root, tilesMesh, overlayMr));
                }
        }

        private void PruneViewsOutsideCurrentWindow()
        {
            int w = Mathf.Max(1, viewChunksSize.x);
            int h = Mathf.Max(1, viewChunksSize.y);

            bool InWindow(ChunkCoord cc) =>
                cc.X >= viewChunkMin.x && cc.X < viewChunkMin.x + w &&
                cc.Y >= viewChunkMin.y && cc.Y < viewChunkMin.y + h;

            var keys = new List<ChunkCoord>(_views.Keys);
            foreach (var cc in keys)
            {
                if (InWindow(cc))
                    continue;

                if (_views.TryGetValue(cc, out var view))
                {
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

                _views.Remove(cc);
            }
        }

        private void ForceRebuildAll()
        {
            foreach (var cc in _views.Keys)
                RebuildChunkTiles(cc);
        }

        private void RebuildChunkTiles(ChunkCoord cc)
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

            RebuildChunkOverlay(cc);
        }

        private void RefreshAllOverlays()
        {
            foreach (var cc in _views.Keys)
                RebuildChunkOverlay(cc);
        }

        private void RebuildChunkOverlay(ChunkCoord cc)
        {
            if (!_views.TryGetValue(cc, out var view))
                return;

            if (view.OverlayRenderer == null)
                return;

            bool show = showDebugOverlay;
            view.OverlayRenderer.enabled = show;

            if (!show)
                return;

            bool hasChunk = _world.TryGetChunk(cc, out var chunk);

            Color c;
            if (!hasChunk || chunk == null)
                c = debugOverlayMissing;
            else if (chunk.StorageKind == ChunkStorageKind.Uniform)
                c = debugOverlayUniform;
            else
                c = debugOverlayDense;

            c.a = Mathf.Clamp01(debugOverlayAlpha);

            var mpb = new MaterialPropertyBlock();
            view.OverlayRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(ColorPropId, c);
            view.OverlayRenderer.SetPropertyBlock(mpb);
        }

        private bool IsInView(ChunkCoord cc)
        {
            int w = Mathf.Max(1, viewChunksSize.x);
            int h = Mathf.Max(1, viewChunksSize.y);

            return cc.X >= viewChunkMin.x && cc.Y >= viewChunkMin.y &&
                   cc.X < viewChunkMin.x + w && cc.Y < viewChunkMin.y + h;
        }

        private static Material CreateRuntimeOverlayMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
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

        private static void BuildSolidQuad(Mesh mesh, int chunkSize, float cellSize)
        {
            float w = chunkSize * cellSize;
            float h = chunkSize * cellSize;

            var verts = new List<Vector3>(4)
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(w, 0f, 0f),
                new Vector3(w, h, 0f),
                new Vector3(0f, h, 0f)
            };

            var tris = new List<int>(6)
            {
                0, 2, 1,
                0, 3, 2
            };

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
        }

        private void ClearAllDirtyRenderFlags()
        {
            var tmp = ListPool<ChunkCoord>.Get();
            tmp.AddRange(_world.DirtyRenderChunks);

            foreach (var cc in tmp)
                _world.ClearDirtyRender(cc);

            ListPool<ChunkCoord>.Release(tmp);
        }

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
    }
}
