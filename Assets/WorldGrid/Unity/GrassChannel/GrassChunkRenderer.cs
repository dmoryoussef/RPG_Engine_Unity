using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using Grass;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.Tiles;
using WorldGrid.Runtime.World;
using WorldGrid.Unity.Rendering;

namespace WorldGrid.Unity.Rendering
{
    /// <summary>
    /// MVP chunk-based grass renderer for SparseChunkWorld.
    /// - Samples Chunk.Get(localX, localY)
    /// - Uses TileLibrary.Channels.Grass (GrassTileChannel)
    /// - Follows ChunkWorldRenderer view window (ViewChunkMin/ViewChunksSize)
    /// - Uploads GrassInfluencer array to shader
    ///
    /// NOTE: ITileLibraryView API is not assumed; we reflect to find a TileLibrary property once at startup.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChunkGrassRenderer : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] private WorldHost worldHost;
        [SerializeField] private ChunkWorldRenderer chunkWorldRenderer;

        [Header("Tile Library")]
        [Tooltip("Concrete TileLibraryProvider (avoid assuming interface members).")]
        [SerializeField] private TileLibraryProvider tileLibraryProvider;
        [SerializeField] private TileLibraryKey tileLibraryKey;

        [Header("Rendering")]
        [SerializeField] private Mesh clumpMesh;
        [SerializeField] private Material grassMaterial;
        [SerializeField] private float grassZ = -0.05f;
        [SerializeField] private bool castShadows = false;
        [SerializeField] private bool receiveShadows = false;

        [Header("Density")]
        [Range(0f, 8f)]
        [SerializeField] private float clumpsPerTile = 1.5f;
        [SerializeField] private int maxClumpsPerChunk = 60000;

        [Header("Placement")]
        [Range(0f, 0.45f)]
        [SerializeField] private float tilePadding = 0.12f;
        [SerializeField] private float scaleMin = 0.9f;
        [SerializeField] private float scaleMax = 1.15f;

        [Header("Determinism")]
        [SerializeField] private int globalSeed = 12345;

        [Header("Wind")]
        [SerializeField] private Vector3 windDirection = new Vector3(1, 0, 0);
        [Range(0f, 1f)][SerializeField] private float windAmplitude = 0.25f;
        [Range(0f, 8f)][SerializeField] private float windFrequency = 2.0f;

        [Header("Influencers")]
        [SerializeField] private int maxInfluencers = 16;

        // ---- runtime ----
        private SparseChunkWorld _world;
        private TileLibrary _tileLibrary;
        private GrassTileChannel _grassChannel;

        private readonly Dictionary<ChunkCoord, ChunkGrassCache> _cache = new();
        private readonly HashSet<ChunkCoord> _dirtyChunks = new();
        private MaterialPropertyBlock _mpb;

        private readonly List<GrassInfluencer> _influencers = new();

        // shader ids (must match your grass shader property names)
        private static readonly int ID_InfluencerCount = Shader.PropertyToID("_InfluencerCount");
        private static readonly int ID_Influencers = Shader.PropertyToID("_Influencers");
        private static readonly int ID_InfluencerStrength = Shader.PropertyToID("_InfluencerStrength");
        private static readonly int ID_WindDir = Shader.PropertyToID("_WindDir");
        private static readonly int ID_WindAmp = Shader.PropertyToID("_WindAmp");
        private static readonly int ID_WindFreq = Shader.PropertyToID("_WindFreq");

        private int ChunkSize => _world != null ? _world.ChunkSize : 0;
        private float CellSize => worldHost != null ? worldHost.CellSize : 1f;
        private Transform WorldRoot => worldHost != null ? worldHost.WorldRoot : transform;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (!TryResolveWorld())
            {
                enabled = false;
                return;
            }

            if (!TryResolveTileLibrary(out var err))
            {
                UnityEngine.Debug.LogError(err, this);
                enabled = false;
                return;
            }

            // Subscribe to SparseChunkWorld.TileUpdated event (nested event args type). :contentReference[oaicite:5]{index=5}
            _world.TileUpdated += OnTileUpdated;

            MarkAllVisibleDirty();
        }

        private void OnDisable()
        {
            if (_world != null)
                _world.TileUpdated -= OnTileUpdated;

            _cache.Clear();
            _dirtyChunks.Clear();
        }

        public void SetInfluencers(List<GrassInfluencer> points)
        {
            _influencers.Clear();
            if (points == null) return;

            int count = Mathf.Min(points.Count, Mathf.Max(0, maxInfluencers));
            for (int i = 0; i < count; i++)
                _influencers.Add(points[i]);
        }

        private void LateUpdate()
        {
            if (_world == null || _tileLibrary == null || _grassChannel == null)
                return;

            if (clumpMesh == null || grassMaterial == null)
                return;

            UploadGlobals();
            RebuildDirtyVisibleChunks();
            DrawVisibleChunks();
        }

        private bool TryResolveWorld()
        {
            if (worldHost == null)
            {
                UnityEngine.Debug.LogError("ChunkGrassRenderer disabled: worldHost is null.", this);
                return false;
            }

            _world = worldHost.World;
            if (_world == null)
            {
                UnityEngine.Debug.LogError("ChunkGrassRenderer disabled: worldHost.World is null.", this);
                return false;
            }

            if (chunkWorldRenderer == null)
            {
                UnityEngine.Debug.LogError("ChunkGrassRenderer disabled: chunkWorldRenderer is null (needed for view window).", this);
                return false;
            }

            return true;
        }

        private bool TryResolveTileLibrary(out string error)
        {
            error = null;

            if (tileLibraryProvider == null)
            {
                error = "ChunkGrassRenderer disabled: tileLibraryProvider is null.";
                return false;
            }

            if (tileLibraryKey.IsEmpty)
            {
                error = "ChunkGrassRenderer disabled: tileLibraryKey is empty.";
                return false;
            }

            // Use concrete provider API (no interface assumptions). :contentReference[oaicite:6]{index=6}
            object viewObj;
            try
            {
                viewObj = tileLibraryProvider.Get(tileLibraryKey);
            }
            catch (Exception ex)
            {
                error = $"ChunkGrassRenderer disabled: provider threw resolving '{tileLibraryKey.Value}': {ex.Message}";
                return false;
            }

            if (viewObj == null)
            {
                error = $"ChunkGrassRenderer disabled: provider returned null view for '{tileLibraryKey.Value}'.";
                return false;
            }

            // Extract TileLibrary from the view via reflection (one-time). This avoids assuming ITileLibraryView shape.
            _tileLibrary = ExtractTileLibraryFromView(viewObj);
            if (_tileLibrary == null)
            {
                error =
                    "ChunkGrassRenderer disabled: could not extract TileLibrary from view. " +
                    "Expected a property/field named one of: Library, TileLibrary, RuntimeLib, RuntimeLibrary.";
                return false;
            }

            // Channel is compiled inside TileLibrary per our pattern.
            _grassChannel = _tileLibrary.Channels?.Grass;
            if (_grassChannel == null)
            {
                error = "ChunkGrassRenderer disabled: tileLibrary.Channels.Grass is null (did channels build?).";
                return false;
            }

            return true;
        }

        private static TileLibrary ExtractTileLibraryFromView(object viewObj)
        {
            // Try common property names
            var t = viewObj.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            string[] propNames = { "Library", "TileLibrary", "RuntimeLib", "RuntimeLibrary" };
            foreach (var name in propNames)
            {
                var p = t.GetProperty(name, flags);
                if (p != null && typeof(TileLibrary).IsAssignableFrom(p.PropertyType))
                    return p.GetValue(viewObj) as TileLibrary;
            }

            // Try common field names
            string[] fieldNames = { "Library", "TileLibrary", "runtimeLib", "_runtimeLib", "RuntimeLib", "RuntimeLibrary" };
            foreach (var name in fieldNames)
            {
                var f = t.GetField(name, flags);
                if (f != null && typeof(TileLibrary).IsAssignableFrom(f.FieldType))
                    return f.GetValue(viewObj) as TileLibrary;
            }

            return null;
        }

        // Correct handler signature for Action<SparseChunkWorld.TileUpdatedEvent>. :contentReference[oaicite:7]{index=7}
        private void OnTileUpdated(SparseChunkWorld.TileUpdatedEvent e)
        {
            _dirtyChunks.Add(e.Chunk);
        }

        private void MarkAllVisibleDirty()
        {
            var min = chunkWorldRenderer.ViewChunkMin;
            var size = chunkWorldRenderer.ViewChunksSize;

            for (int dy = 0; dy < size.y; dy++)
                for (int dx = 0; dx < size.x; dx++)
                    _dirtyChunks.Add(new ChunkCoord(min.X + dx, min.Y + dy));
        }

        private void RebuildDirtyVisibleChunks()
        {
            var min = chunkWorldRenderer.ViewChunkMin;
            var size = chunkWorldRenderer.ViewChunksSize;

            if (size.x <= 0 || size.y <= 0)
                return;

            _tmpToRebuild.Clear();

            foreach (var cc in _dirtyChunks)
            {
                if (cc.X < min.X || cc.Y < min.Y) continue;
                if (cc.X >= min.X + size.x || cc.Y >= min.Y + size.y) continue;
                _tmpToRebuild.Add(cc);
            }

            for (int i = 0; i < _tmpToRebuild.Count; i++)
            {
                var cc = _tmpToRebuild[i];
                BuildChunkCache(cc);
                _dirtyChunks.Remove(cc);
            }
        }

        private readonly List<ChunkCoord> _tmpToRebuild = new(256);

        private void BuildChunkCache(ChunkCoord cc)
        {
            // Chunk type is WorldGrid.Runtime.Chunks.Chunk. 
            if (!_world.TryGetChunk(cc, out var chunk) || chunk == null)
            {
                _cache.Remove(cc);
                return;
            }

            if (!_cache.TryGetValue(cc, out var cache))
            {
                cache = new ChunkGrassCache();
                _cache[cc] = cache;
            }

            cache.Build(
                chunk: chunk,
                chunkCoord: cc,
                chunkSize: ChunkSize,
                cellSize: CellSize,
                worldRoot: WorldRoot,
                grassChannel: _grassChannel,
                clumpsPerTile: clumpsPerTile,
                tilePadding: tilePadding,
                scaleMin: scaleMin,
                scaleMax: scaleMax,
                globalSeed: globalSeed,
                grassZ: grassZ,
                maxClumpsPerChunk: maxClumpsPerChunk
            );
        }

        private void DrawVisibleChunks()
        {
            var min = chunkWorldRenderer.ViewChunkMin;
            var size = chunkWorldRenderer.ViewChunksSize;

            if (size.x <= 0 || size.y <= 0)
                return;

            var shadowMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            for (int dy = 0; dy < size.y; dy++)
                for (int dx = 0; dx < size.x; dx++)
                {
                    var cc = new ChunkCoord(min.X + dx, min.Y + dy);
                    if (!_cache.TryGetValue(cc, out var cache) || cache.InstanceCount == 0)
                        continue;

                    cache.Draw(clumpMesh, grassMaterial, _mpb, shadowMode, receiveShadows);
                }
        }

        private void UploadGlobals()
        {
            Vector3 wd = windDirection.sqrMagnitude > 0.0001f ? windDirection.normalized : Vector3.right;
            _mpb.SetVector(ID_WindDir, new Vector4(wd.x, wd.y, wd.z, 0f));
            _mpb.SetFloat(ID_WindAmp, windAmplitude);
            _mpb.SetFloat(ID_WindFreq, windFrequency);

            int cap = Mathf.Max(0, maxInfluencers);
            int count = Mathf.Min(_influencers.Count, cap);

            var posRad = new Vector4[cap];
            var strength = new float[cap];

            for (int i = 0; i < count; i++)
            {
                // GrassInfluencer fields are lowercase: position/radius/strength. :contentReference[oaicite:9]{index=9}
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

        // ----------------- Chunk cache -----------------

        private sealed class ChunkGrassCache
        {
            private const int BatchMax = 1023;

            private readonly List<Matrix4x4[]> _matBatches = new();
            private readonly List<int> _batchCounts = new();
            private int _count;

            public int InstanceCount => _count;

            public void Build(
                WorldGrid.Runtime.Chunks.Chunk chunk,
                ChunkCoord chunkCoord,
                int chunkSize,
                float cellSize,
                Transform worldRoot,
                GrassTileChannel grassChannel,
                float clumpsPerTile,
                float tilePadding,
                float scaleMin,
                float scaleMax,
                int globalSeed,
                float grassZ,
                int maxClumpsPerChunk)
            {
                _matBatches.Clear();
                _batchCounts.Clear();
                _count = 0;

                int worldX0 = chunkCoord.X * chunkSize;
                int worldY0 = chunkCoord.Y * chunkSize;

                var mats = new List<Matrix4x4>(Mathf.Min(maxClumpsPerChunk, 8192));

                for (int y = 0; y < chunkSize; y++)
                    for (int x = 0; x < chunkSize; x++)
                    {
                        int tileId = chunk.Get(x, y);

                        if (!grassChannel.IsGrassable(tileId))
                            continue;

                        // GrassTileChannel exposes Info via Get(tileId). :contentReference[oaicite:10]{index=10}
                        var info = grassChannel.Get(tileId);
                        float mult = Mathf.Max(0f, info.density);
                        if (mult <= 0f)
                            continue;

                        float d = clumpsPerTile * mult;
                        int n0 = Mathf.FloorToInt(d);
                        float f = Mathf.Clamp01(d - n0);

                        uint seed = HashToUint(globalSeed, worldX0 + x, worldY0 + y);
                        var rng = new XorShift32(seed);

                        int count = n0 + (rng.NextFloat01() < f ? 1 : 0);
                        if (count <= 0) continue;

                        for (int i = 0; i < count; i++)
                        {
                            if (mats.Count >= maxClumpsPerChunk)
                                goto BUILD_DONE;

                            float jx = rng.Range(-0.5f + tilePadding, 0.5f - tilePadding);
                            float jy = rng.Range(-0.5f + tilePadding, 0.5f - tilePadding);

                            float wx = (worldX0 + x + 0.5f + jx) * cellSize;
                            float wy = (worldY0 + y + 0.5f + jy) * cellSize;

                            Vector3 pos = new Vector3(wx, wy, grassZ);
                            if (worldRoot != null)
                                pos = worldRoot.TransformPoint(pos);

                            float rotDeg = rng.Range(0f, 360f);
                            float scl = rng.Range(scaleMin, scaleMax);

                            mats.Add(Matrix4x4.TRS(
                                pos,
                                Quaternion.AngleAxis(rotDeg, Vector3.forward),
                                Vector3.one * scl));
                        }
                    }

                BUILD_DONE:
                _count = mats.Count;

                int offset = 0;
                while (offset < mats.Count)
                {
                    int n = Mathf.Min(BatchMax, mats.Count - offset);
                    var batch = new Matrix4x4[n];
                    for (int i = 0; i < n; i++)
                        batch[i] = mats[offset + i];

                    _matBatches.Add(batch);
                    _batchCounts.Add(n);
                    offset += n;
                }
            }

            public void Draw(Mesh mesh, Material mat, MaterialPropertyBlock mpb, ShadowCastingMode shadowMode, bool receiveShadows)
            {
                for (int i = 0; i < _matBatches.Count; i++)
                {
                    Graphics.DrawMeshInstanced(
                        mesh, 0, mat,
                        _matBatches[i], _batchCounts[i],
                        mpb,
                        shadowMode,
                        receiveShadows);
                }
            }

            private static uint HashToUint(int seed, int x, int y)
            {
                unchecked
                {
                    uint h = 2166136261u;
                    h = (h ^ (uint)seed) * 16777619u;
                    h = (h ^ (uint)x) * 16777619u;
                    h = (h ^ (uint)y) * 16777619u;
                    h ^= h >> 13;
                    h *= 1274126177u;
                    h ^= h >> 16;
                    return h;
                }
            }

            private struct XorShift32
            {
                private uint _state;
                public XorShift32(uint seed) { _state = seed == 0 ? 0x6C8E9CF5u : seed; }

                public uint NextU()
                {
                    uint x = _state;
                    x ^= x << 13;
                    x ^= x >> 17;
                    x ^= x << 5;
                    _state = x;
                    return x;
                }

                public float NextFloat01() => (NextU() & 0x00FFFFFFu) / 16777216f;
                public float Range(float a, float b) => a + (b - a) * NextFloat01();
            }
        }
    }
}
