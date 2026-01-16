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
        [SerializeField] private TileLibraryAsset tileLibraryAsset;

        [Header("Default / Empty Background (Sprite)")]
        [Tooltip("Optional sprite used to visualize default/empty space. If null, no background is drawn.")]
        [SerializeField] private Sprite defaultBackgroundSprite;

        [Tooltip("Optional shader used for the background sprite material. If null, uses Shader.Find(\"Unlit/Texture\").")]
        [SerializeField] private Shader defaultBackgroundShader;

        [Header("Rendering")]
        [Tooltip("Draw a single background quad per chunk (uses Default Background Sprite).")]
        [SerializeField] private bool renderDefaultBackground = true;

        [Tooltip("Chunks rendered in X and Y (width/height).")]
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
        private TileLibrary _tileLibrary;

        private Material _defaultBackgroundRuntimeMaterial;
        private Material _debugOverlayMaterial;

        private readonly Dictionary<ChunkCoord, ChunkView> _views = new();

        private readonly MeshData _tilesMeshData = new();
        private readonly MeshData _bgMeshData = new();

        private bool _lastShowDebugOverlay;
        private float _lastDebugOverlayAlpha;

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

            if (tileLibraryAsset == null)
            {
                UnityEngine.Debug.LogError("ChunkWorldRenderer: tileLibraryAsset not assigned.", this);
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

            if (tileLibraryAsset.atlasMaterial == null)
            {
                UnityEngine.Debug.LogError("ChunkWorldRenderer: tileLibraryAsset.atlasMaterial is not assigned.", this);
                enabled = false;
                return;
            }

            _tileLibrary = tileLibraryAsset.BuildRuntime();

            if (renderDefaultBackground && defaultBackgroundSprite != null)
                _defaultBackgroundRuntimeMaterial = CreateRuntimeSpriteMaterial(defaultBackgroundSprite);

            // IMPORTANT: Use a blending-capable shader so alpha actually blends.
            _debugOverlayMaterial = CreateRuntimeOverlayMaterial();

            EnsureViewChunkGameObjectsExist();
            ForceRebuildAll();

            // Cache settings so toggles update immediately in play mode.
            _lastShowDebugOverlay = showDebugOverlay;
            _lastDebugOverlayAlpha = debugOverlayAlpha;

            // Ensure overlays reflect current toggles/colors immediately.
            RefreshAllOverlays();
        }

        private void OnDestroy()
        {
            if (_defaultBackgroundRuntimeMaterial != null)
            {
                Destroy(_defaultBackgroundRuntimeMaterial);
                _defaultBackgroundRuntimeMaterial = null;
            }

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

            // If overlay settings changed, refresh overlays immediately.
            if (_lastShowDebugOverlay != showDebugOverlay
                || Mathf.Abs(_lastDebugOverlayAlpha - debugOverlayAlpha) > 0.0001f)
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

        /// <summary>
        /// Set the bottom-left chunk coordinate of the renderer view window.
        /// IMPORTANT: this must create missing view objects for newly-visible chunks,
        /// otherwise nudging only moves debug outlines and nothing is rendered.
        /// </summary>
        public void SetViewChunkMin(ChunkCoord min, bool pruneViewsOutsideWindow = false)
        {
            viewChunkMin = new ChunkCoordSerializable(min.X, min.Y);

            if (_world == null)
                return;

            // Create views for newly-visible chunk coords.
            EnsureViewChunkGameObjectsExist();

            // Optional: destroy views outside the current window to prevent buildup.
            if (pruneViewsOutsideWindow)
                PruneViewsOutsideCurrentWindow();

            // Rebuild all chunk meshes and overlays for the current views.
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

        private void PruneViewsOutsideCurrentWindow()
        {
            ChunkCoord min = ViewChunkMin;

            int w = Mathf.Max(1, viewChunksSize.x);
            int h = Mathf.Max(1, viewChunksSize.y);

            bool InWindow(ChunkCoord cc) =>
                cc.X >= min.X && cc.X < min.X + w &&
                cc.Y >= min.Y && cc.Y < min.Y + h;

            var keys = new List<ChunkCoord>(_views.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                var cc = keys[i];
                if (InWindow(cc))
                    continue;

                if (_views.TryGetValue(cc, out var view))
                {
                    if (view.BackgroundMesh != null)
                        Destroy(view.BackgroundMesh);

                    if (view.TilesMesh != null)
                        Destroy(view.TilesMesh);

                    // Overlay mesh is owned by the MeshFilter on the Overlay GO, not stored directly.
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

        private void RefreshAllOverlays()
        {
            foreach (var kv in _views)
                RebuildChunkOverlay(kv.Key);
        }

        private void ClearAllDirtyRenderFlags()
        {
            var tmp = ListPool<ChunkCoord>.Get();
            tmp.AddRange(_world.DirtyRenderChunks);

            for (int i = 0; i < tmp.Count; i++)
                _world.ClearDirtyRender(tmp[i]);

            ListPool<ChunkCoord>.Release(tmp);
        }

        private void EnsureViewChunkGameObjectsExist()
        {
            var min = new ChunkCoord(viewChunkMin.x, viewChunkMin.y);

            int w = Mathf.Max(1, viewChunksSize.x);
            int h = Mathf.Max(1, viewChunksSize.y);

            bool canDrawBackground = renderDefaultBackground
                                     && defaultBackgroundSprite != null
                                     && _defaultBackgroundRuntimeMaterial != null;

            // IMPORTANT: Always create overlays if we have a material,
            // so toggling showDebugOverlay at runtime works both directions.
            bool canCreateOverlay = _debugOverlayMaterial != null;

            float cs = CellSize;

            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                {
                    var cc = new ChunkCoord(min.X + dx, min.Y + dy);
                    if (_views.ContainsKey(cc))
                        continue;

                    var chunkRoot = new GameObject($"Chunk_{cc.X}_{cc.Y}");
                    chunkRoot.transform.SetParent(WorldRoot, worldPositionStays: false);

                    chunkRoot.transform.localPosition = new Vector3(
                        cc.X * _world.ChunkSize * cs,
                        cc.Y * _world.ChunkSize * cs,
                        0f
                    );

                    chunkRoot.transform.localRotation = Quaternion.identity;
                    chunkRoot.transform.localScale = Vector3.one;

                    // Background child
                    Mesh bgMesh = null;
                    if (canDrawBackground)
                    {
                        var bgGo = new GameObject("DefaultBG");
                        bgGo.transform.SetParent(chunkRoot.transform, worldPositionStays: false);
                        bgGo.transform.localPosition = Vector3.zero;
                        bgGo.transform.localRotation = Quaternion.identity;
                        bgGo.transform.localScale = Vector3.one;

                        var bgMf = bgGo.AddComponent<MeshFilter>();
                        var bgMr = bgGo.AddComponent<MeshRenderer>();
                        bgMr.sharedMaterial = _defaultBackgroundRuntimeMaterial;

                        bgMesh = new Mesh { name = $"ChunkBG_{cc.X}_{cc.Y}" };
                        bgMf.sharedMesh = bgMesh;

                        BuildBackgroundQuadFromSprite(bgMesh, _world.ChunkSize, cs, defaultBackgroundSprite);
                    }

                    // Foreground child (atlas tiles)
                    var tilesGo = new GameObject("Tiles");
                    tilesGo.transform.SetParent(chunkRoot.transform, worldPositionStays: false);
                    tilesGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                    tilesGo.transform.localRotation = Quaternion.identity;
                    tilesGo.transform.localScale = Vector3.one;

                    var tilesMf = tilesGo.AddComponent<MeshFilter>();
                    var tilesMr = tilesGo.AddComponent<MeshRenderer>();
                    tilesMr.sharedMaterial = tileLibraryAsset.atlasMaterial;

                    tilesMr.sortingLayerName = "Tiles";
                    tilesMr.sortingOrder = 0;

                    var tilesMesh = new Mesh { name = $"ChunkTiles_{cc.X}_{cc.Y}" };
                    tilesMf.sharedMesh = tilesMesh;

                    // Debug overlay child (tint quad)
                    MeshRenderer overlayMr = null;
                    if (canCreateOverlay)
                    {
                        var overlayGo = new GameObject("DebugOverlay");
                        overlayGo.transform.SetParent(chunkRoot.transform, worldPositionStays: false);

                        // Place behind tiles (more negative is farther back in 2D camera looking down -Z),
                        // but note transparency queue can still visually overlay. Alpha blending now works.
                        overlayGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);

                        overlayGo.transform.localRotation = Quaternion.identity;
                        overlayGo.transform.localScale = Vector3.one;

                        var overlayMf = overlayGo.AddComponent<MeshFilter>();
                        overlayMr = overlayGo.AddComponent<MeshRenderer>();
                        overlayMr.sharedMaterial = _debugOverlayMaterial;

                        var overlayMesh = new Mesh { name = $"ChunkOverlay_{cc.X}_{cc.Y}" };
                        overlayMf.sharedMesh = overlayMesh;

                        BuildSolidQuad(overlayMesh, _world.ChunkSize, cs);
                    }

                    _views.Add(cc, new ChunkView(chunkRoot, bgMesh, tilesMesh, overlayMr));
                }
        }

        private void ForceRebuildAll()
        {
            foreach (var kv in _views)
                RebuildChunkTiles(kv.Key);
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

        private void RebuildChunkOverlay(ChunkCoord cc)
        {
            if (!_views.TryGetValue(cc, out var view))
                return;

            if (view.OverlayRenderer == null)
                return;

            view.OverlayRenderer.enabled = showDebugOverlay;

            if (!showDebugOverlay)
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

        private Material CreateRuntimeSpriteMaterial(Sprite sprite)
        {
            Shader shader = defaultBackgroundShader != null
                ? defaultBackgroundShader
                : Shader.Find("Unlit/Texture");

            if (shader == null)
            {
                UnityEngine.Debug.LogError("ChunkWorldRenderer: Could not find shader for default background (Unlit/Texture).", this);
                return null;
            }

            var mat = new Material(shader)
            {
                name = "WorldGrid_DefaultBackground_RuntimeMat"
            };

            mat.mainTexture = sprite.texture;

            return mat;
        }

        private static Material CreateRuntimeOverlayMaterial()
        {
            // Sprites/Default blends alpha correctly in Built-in and most pipelines.
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");

            if (shader == null)
            {
                UnityEngine.Debug.LogError("ChunkWorldRenderer: Could not find an alpha-blending shader for DebugOverlay (Sprites/Default).");
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

        private static void BuildBackgroundQuadFromSprite(Mesh mesh, int chunkSize, float cellSize, Sprite sprite)
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

            Rect tr = sprite.textureRect;
            Texture tex = sprite.texture;

            float uMin = tr.xMin / tex.width;
            float vMin = tr.yMin / tex.height;
            float uMax = tr.xMax / tex.width;
            float vMax = tr.yMax / tex.height;

            var uvs = new List<Vector2>(4)
            {
                new Vector2(uMin, vMin),
                new Vector2(uMax, vMin),
                new Vector2(uMax, vMax),
                new Vector2(uMin, vMax)
            };

            var tris = new List<int>(6)
            {
                0, 2, 1,
                0, 3, 2
            };

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
        }

        private bool IsInView(ChunkCoord cc)
        {
            var min = new ChunkCoord(viewChunkMin.x, viewChunkMin.y);

            int w = Mathf.Max(1, viewChunksSize.x);
            int h = Mathf.Max(1, viewChunksSize.y);

            return cc.X >= min.X && cc.Y >= min.Y
                && cc.X < min.X + w
                && cc.Y < min.Y + h;
        }

        private sealed class ChunkView
        {
            public GameObject Root { get; }
            public Mesh BackgroundMesh { get; }
            public Mesh TilesMesh { get; }
            public MeshRenderer OverlayRenderer { get; }

            public ChunkView(GameObject root, Mesh backgroundMesh, Mesh tilesMesh, MeshRenderer overlayRenderer)
            {
                Root = root;
                BackgroundMesh = backgroundMesh;
                TilesMesh = tilesMesh;
                OverlayRenderer = overlayRenderer;
            }
        }

        [System.Serializable]
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
