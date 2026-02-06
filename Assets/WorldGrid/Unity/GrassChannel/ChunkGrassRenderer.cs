using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.Tiles;
using WorldGrid.Runtime.World;

namespace WorldGrid.Unity.Rendering
{
    [DisallowMultipleComponent]
    public sealed class ChunkGrassRenderer : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] private WorldHost worldHost;
        [SerializeField] private ChunkWorldRenderer chunkWorldRenderer;

        [Header("Tile Library")]
        [SerializeField] private TileLibraryProvider tileLibraryProvider;
        [SerializeField] private TileLibraryKey tileLibraryKey;

        [Header("Profile")]
        [SerializeField] private Grass.GrassRenderProfile profile;

        [Header("Influencers")]
        [SerializeField] private int maxInfluencers = 16;

        [Header("Binding")]
        [SerializeField] private float bindRetrySeconds = 0.25f;
        [SerializeField] private bool logBind = true;

        // ---- runtime ----
        private SparseChunkWorld _world;
        private TileLibrary _tileLibrary;
        private GrassTileChannel _grassChannel;

        private readonly Dictionary<ChunkCoord, ChunkGrassCache> _cache = new();
        private readonly HashSet<ChunkCoord> _dirtyChunks = new();
        private readonly List<ChunkCoord> _tmpToRebuild = new(256);

        private MaterialPropertyBlock _mpb;
        private readonly List<Grass.GrassInfluencer> _influencers = new();

        private bool _bound;
        private float _nextBindAttemptTime;
        private bool _loggedBind;

        private Material _runtimeMaterial;

        // shader ids
        private static readonly int ID_MainTex = Shader.PropertyToID("_MainTex");

        private static readonly int ID_InfluencerCount = Shader.PropertyToID("_InfluencerCount");
        private static readonly int ID_Influencers = Shader.PropertyToID("_Influencers");
        private static readonly int ID_InfluencerStrength = Shader.PropertyToID("_InfluencerStrength");

        private static readonly int ID_WindDir = Shader.PropertyToID("_WindDir");
        private static readonly int ID_WindAmp = Shader.PropertyToID("_WindAmp");
        private static readonly int ID_WindFreq = Shader.PropertyToID("_WindFreq");
        private static readonly int ID_WindWorldScale = Shader.PropertyToID("_WindWorldScale");

        private static readonly int ID_PatchColorA = Shader.PropertyToID("_PatchColorA");
        private static readonly int ID_PatchColorB = Shader.PropertyToID("_PatchColorB");
        private static readonly int ID_PatchScale = Shader.PropertyToID("_PatchScale");
        private static readonly int ID_PatchStrength = Shader.PropertyToID("_PatchStrength");
        private static readonly int ID_UseXZPlane = Shader.PropertyToID("_UseXZPlane");

        private static readonly int ID_BaseStiffness = Shader.PropertyToID("_BaseStiffness");
        private static readonly int ID_BendExponent = Shader.PropertyToID("_BendExponent");
        private static readonly int ID_SwayStrength = Shader.PropertyToID("_SwayStrength");
        private static readonly int ID_TipBoost = Shader.PropertyToID("_TipBoost");

        private static readonly int ID_EmissionStrength = Shader.PropertyToID("_EmissionStrength");

        private Vector4[] _infPosRadEmpty;
        private float[] _infStrengthEmpty;

        private int ChunkSize => _world != null ? _world.ChunkSize : 0;
        private float CellSize => worldHost != null ? worldHost.CellSize : 1f;
        private Transform WorldRoot => worldHost != null ? worldHost.WorldRoot : transform;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();

            int cap = Mathf.Max(0, maxInfluencers);
            _infPosRadEmpty = new Vector4[cap];
            _infStrengthEmpty = new float[cap];
        }

        private void OnEnable()
        {
            _bound = false;
            _loggedBind = false;
            _nextBindAttemptTime = 0f;

            _cache.Clear();
            _dirtyChunks.Clear();

            EnsureRuntimeMaterial();
        }

        private void OnDisable()
        {
            if (_bound && _world != null)
                _world.TileUpdated -= OnTileUpdated;

            _bound = false;
            _cache.Clear();
            _dirtyChunks.Clear();

            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

        public void SetInfluencers(List<Grass.GrassInfluencer> points)
        {
            _influencers.Clear();
            if (points == null) return;

            int count = Mathf.Min(points.Count, Mathf.Max(0, maxInfluencers));
            for (int i = 0; i < count; i++)
                _influencers.Add(points[i]);
        }

        private void LateUpdate()
        {
            if (profile == null)
                return;

            EnsureRuntimeMaterial();
            if (_runtimeMaterial == null)
                return;

            if (!_bound)
            {
                TryBind();
                if (!_bound) return;
            }

            if (_world == null || _tileLibrary == null || _grassChannel == null)
                return;

            if (profile.EffectiveMesh == null)
                return;

            UploadGlobalsFromProfile();
            RebuildDirtyVisibleChunks();
            DrawVisibleChunksSorted();
        }

        private void EnsureRuntimeMaterial()
        {
            if (profile == null) return;
            if (_runtimeMaterial != null) return;

            if (!profile.IsValid(out _))
                return;

            _runtimeMaterial = new Material(profile.materialTemplate)
            {
                name = $"{profile.materialTemplate.name} (Runtime)"
            };

            if (profile.renderQueueOverride >= 0)
                _runtimeMaterial.renderQueue = profile.renderQueueOverride;

            if (profile.mainTextureOverride != null)
                _runtimeMaterial.SetTexture(ID_MainTex, profile.mainTextureOverride);
        }

        private void TryBind()
        {
            if (Time.unscaledTime < _nextBindAttemptTime)
                return;

            _nextBindAttemptTime = Time.unscaledTime + Mathf.Max(0.05f, bindRetrySeconds);

            if (worldHost == null || worldHost.World == null) return;
            if (chunkWorldRenderer == null) return;
            if (tileLibraryProvider == null || !tileLibraryProvider.IsReady) return;
            if (tileLibraryKey.IsEmpty) return;

            _world = worldHost.World;

            if (!TryResolveTileLibrary(out _))
                return;

            var size = chunkWorldRenderer.ViewChunksSize;
            if (size.x <= 0 || size.y <= 0)
                return;

            _world.TileUpdated += OnTileUpdated;
            MarkAllVisibleDirty();

            _bound = true;

            if (logBind && !_loggedBind)
            {
                _loggedBind = true;
                UnityEngine.Debug.Log($"ChunkGrassRenderer bound. chunkSize={_world.ChunkSize}, view={chunkWorldRenderer.ViewChunksSize}", this);
            }
        }

        private bool TryResolveTileLibrary(out string error)
        {
            error = null;

            object viewObj;
            try { viewObj = tileLibraryProvider.Get(tileLibraryKey); }
            catch (Exception ex)
            {
                error = $"ChunkGrassRenderer: provider threw resolving '{tileLibraryKey.Value}': {ex.Message}";
                return false;
            }

            if (viewObj == null)
            {
                error = $"ChunkGrassRenderer: provider returned null view for '{tileLibraryKey.Value}'.";
                return false;
            }

            _tileLibrary = ExtractTileLibraryFromView(viewObj);
            if (_tileLibrary == null)
            {
                error = "ChunkGrassRenderer: could not extract TileLibrary from view.";
                return false;
            }

            _grassChannel = _tileLibrary.Channels?.Grass;
            if (_grassChannel == null)
            {
                error = "ChunkGrassRenderer: tileLibrary.Channels.Grass is null.";
                return false;
            }

            return true;
        }

        private static TileLibrary ExtractTileLibraryFromView(object viewObj)
        {
            var t = viewObj.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            string[] propNames = { "Library", "TileLibrary", "RuntimeLib", "RuntimeLibrary" };
            foreach (var name in propNames)
            {
                var p = t.GetProperty(name, flags);
                if (p != null && typeof(TileLibrary).IsAssignableFrom(p.PropertyType))
                    return p.GetValue(viewObj) as TileLibrary;
            }

            string[] fieldNames = { "Library", "TileLibrary", "runtimeLib", "_runtimeLib", "RuntimeLib", "RuntimeLibrary" };
            foreach (var name in fieldNames)
            {
                var f = t.GetField(name, flags);
                if (f != null && typeof(TileLibrary).IsAssignableFrom(f.FieldType))
                    return f.GetValue(viewObj) as TileLibrary;
            }

            return null;
        }

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

        private void BuildChunkCache(ChunkCoord cc)
        {
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

            float clumps = profile.clumpsPerTile * Mathf.Max(0.001f, profile.clumpsPerTileMultiplier);

            cache.Build(
                chunk: chunk,
                chunkCoord: cc,
                chunkSize: ChunkSize,
                cellSize: CellSize,
                worldRoot: WorldRoot,
                grassChannel: _grassChannel,
                clumpsPerTile: clumps,
                tilePadding: profile.tilePadding,
                uniformScale: profile.uniformScale,
                scaleWidthMin: profile.scaleWidthMin,
                scaleWidthMax: profile.scaleWidthMax,
                scaleHeightMin: profile.scaleHeightMin,
                scaleHeightMax: profile.scaleHeightMax,
                heightBias: profile.heightBias,
                randomRotation: profile.randomRotation,
                minRotationDeg: profile.minRotationDeg,
                maxRotationDeg: profile.maxRotationDeg,
                useXZPlane: profile.useXZPlane,
                globalSeed: profile.globalSeed,
                grassZ: profile.grassZ,
                maxClumpsPerChunk: profile.maxClumpsPerChunk
            );
        }

        private void DrawVisibleChunksSorted()
        {
            var min = chunkWorldRenderer.ViewChunkMin;
            var size = chunkWorldRenderer.ViewChunksSize;

            if (size.x <= 0 || size.y <= 0)
                return;

            var shadowMode = profile.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            for (int dy = size.y - 1; dy >= 0; dy--)
                for (int dx = 0; dx < size.x; dx++)
                {
                    var cc = new ChunkCoord(min.X + dx, min.Y + dy);

                    if (!_cache.TryGetValue(cc, out var cache) || cache.InstanceCount == 0)
                        continue;

                    cache.Draw(profile.EffectiveMesh, _runtimeMaterial, _mpb, shadowMode, profile.receiveShadows);
                }
        }

        private void UploadGlobalsFromProfile()
        {
            // Wind
            Vector3 wd = profile.windDirection.sqrMagnitude > 0.0001f ? profile.windDirection.normalized : Vector3.right;
            _mpb.SetVector(ID_WindDir, new Vector4(wd.x, wd.y, wd.z, 0f));
            _mpb.SetFloat(ID_WindAmp, profile.windAmplitude);
            _mpb.SetFloat(ID_WindFreq, profile.windFrequency);
            _mpb.SetFloat(ID_WindWorldScale, profile.windWorldScale);

            // Patch
            _mpb.SetColor(ID_PatchColorA, profile.patchColorA);
            _mpb.SetColor(ID_PatchColorB, profile.patchColorB);
            _mpb.SetFloat(ID_PatchScale, profile.patchScale);
            _mpb.SetFloat(ID_PatchStrength, profile.patchStrength);
            _mpb.SetFloat(ID_UseXZPlane, profile.useXZPlane);

            // Bend/Sway
            _mpb.SetFloat(ID_BaseStiffness, profile.baseStiffness);
            _mpb.SetFloat(ID_BendExponent, profile.bendExponent);
            _mpb.SetFloat(ID_SwayStrength, profile.swayStrength);
            _mpb.SetFloat(ID_TipBoost, profile.tipBoost);

            // Lighting (requires shader support)
            _mpb.SetFloat(ID_EmissionStrength, profile.emissionStrength);

            // Influencers (optional)
            int cap = Mathf.Max(0, maxInfluencers);
            if (_infPosRadEmpty == null || _infPosRadEmpty.Length != cap)
            {
                _infPosRadEmpty = new Vector4[cap];
                _infStrengthEmpty = new float[cap];
            }

            int count = Mathf.Min(_influencers.Count, cap);
            if (count <= 0)
            {
                _mpb.SetInt(ID_InfluencerCount, 0);
                if (cap > 0)
                {
                    _mpb.SetVectorArray(ID_Influencers, _infPosRadEmpty);
                    _mpb.SetFloatArray(ID_InfluencerStrength, _infStrengthEmpty);
                }
                return;
            }

            var posRad = new Vector4[cap];
            var strength = new float[cap];

            for (int i = 0; i < count; i++)
            {
                var inf = _influencers[i];
                posRad[i] = new Vector4(inf.position.x, inf.position.y, inf.position.z, inf.radius);
                strength[i] = inf.strength;
            }

            _mpb.SetInt(ID_InfluencerCount, count);
            _mpb.SetVectorArray(ID_Influencers, posRad);
            _mpb.SetFloatArray(ID_InfluencerStrength, strength);
        }

        // ----------------- Chunk cache -----------------

        private sealed class ChunkGrassCache
        {
            private const int BatchMax = 1023;

            private readonly List<Matrix4x4[]> _matBatches = new();
            private readonly List<int> _batchCounts = new();
            private int _count;

            public int InstanceCount => _count;

            private struct Item
            {
                public float y;
                public Matrix4x4 m;
            }

            public void Build(
                WorldGrid.Runtime.Chunks.Chunk chunk,
                ChunkCoord chunkCoord,
                int chunkSize,
                float cellSize,
                Transform worldRoot,
                GrassTileChannel grassChannel,
                float clumpsPerTile,
                float tilePadding,
                bool uniformScale,
                float scaleWidthMin,
                float scaleWidthMax,
                float scaleHeightMin,
                float scaleHeightMax,
                float heightBias,
                bool randomRotation,
                float minRotationDeg,
                float maxRotationDeg,
                float useXZPlane,
                int globalSeed,
                float grassZ,
                int maxClumpsPerChunk)
            {
                _matBatches.Clear();
                _batchCounts.Clear();
                _count = 0;

                int worldX0 = chunkCoord.X * chunkSize;
                int worldY0 = chunkCoord.Y * chunkSize;

                var items = new List<Item>(Mathf.Min(maxClumpsPerChunk, 8192));

                for (int y = 0; y < chunkSize; y++)
                    for (int x = 0; x < chunkSize; x++)
                    {
                        int tileId = chunk.Get(x, y);

                        if (!grassChannel.IsGrassable(tileId))
                            continue;

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
                            if (items.Count >= maxClumpsPerChunk)
                                goto BUILD_DONE;

                            float jx = rng.Range(-0.5f + tilePadding, 0.5f - tilePadding);
                            float jy = rng.Range(-0.5f + tilePadding, 0.5f - tilePadding);

                            float wx = (worldX0 + x + 0.5f + jx) * cellSize;
                            float wy = (worldY0 + y + 0.5f + jy) * cellSize;

                            Vector3 pos = new Vector3(wx, wy, grassZ);
                            if (worldRoot != null)
                                pos = worldRoot.TransformPoint(pos);

                            float rotDeg = 0f;
                            if (randomRotation && !Mathf.Approximately(maxRotationDeg, minRotationDeg))
                                rotDeg = rng.Range(minRotationDeg, maxRotationDeg);

                            Vector3 scale;
                            if (uniformScale)
                            {
                                float s = rng.Range(scaleWidthMin, scaleWidthMax);
                                scale = new Vector3(s, s, s);
                            }
                            else
                            {
                                float sx = rng.Range(scaleWidthMin, scaleWidthMax);
                                float sy = rng.Range(scaleHeightMin, scaleHeightMax) * Mathf.Max(0.001f, heightBias);
                                scale = new Vector3(sx, sy, 1f);
                            }

                            // XY mode rotates around Z; XZ mode rotates around Y
                            Quaternion rot = (useXZPlane > 0.5f)
                                ? Quaternion.AngleAxis(rotDeg, Vector3.up)
                                : Quaternion.AngleAxis(rotDeg, Vector3.forward);

                            var m = Matrix4x4.TRS(pos, rot, scale);
                            items.Add(new Item { y = pos.y, m = m });
                        }
                    }

                BUILD_DONE:
                items.Sort((a, b) => b.y.CompareTo(a.y)); // higher Y first, lower Y last

                _count = items.Count;

                int offset = 0;
                while (offset < items.Count)
                {
                    int n = Mathf.Min(BatchMax, items.Count - offset);
                    var batch = new Matrix4x4[n];

                    for (int i = 0; i < n; i++)
                        batch[i] = items[offset + i].m;

                    _matBatches.Add(batch);
                    _batchCounts.Add(n);
                    offset += n;
                }
            }

            public void Draw(Mesh mesh, Material mat, MaterialPropertyBlock mpb, ShadowCastingMode shadowMode, bool receiveShadows)
            {
                for (int i = 0; i < _matBatches.Count; i++)
                {
                    Graphics.DrawMeshInstanced(mesh, 0, mat, _matBatches[i], _batchCounts[i], mpb, shadowMode, receiveShadows);
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

                public XorShift32(uint seed)
                {
                    _state = seed == 0 ? 0x6C8E9CF5u : seed;
                }

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
