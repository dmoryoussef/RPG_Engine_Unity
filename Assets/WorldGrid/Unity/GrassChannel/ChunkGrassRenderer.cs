using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGrid.Runtime.Chunks;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.Rendering;
using WorldGrid.Runtime.Tiles;
using WorldGrid.Runtime.World;

namespace WorldGrid.Unity.Rendering
{
    /// <summary>
    /// Grass render channel (instanced) driven by the orchestrator.
    /// - View is authoritative via IChunkViewWindowProvider
    /// - Tile libraries are injected via ITileLibraryView (no TileLibraryProvider dependency)
    /// - Z layering is baked into instance positions: (profile.grassZ + grassLayerZ + localZOffset)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChunkGrassRenderer : MonoBehaviour, IRenderChunkChannel
    {
        [Header("Tile Library (resolved by orchestrator)")]
        [SerializeField] private TileLibraryKey tileLibraryKey;

        // Convention-based key consumed by ChunkRenderOrchestrator via reflection.
        public TileLibraryKey TileLibraryKey => tileLibraryKey;

        [Header("Grass")]
        [SerializeField] private Grass.GrassRenderProfile profile;

        [Header("Layering (renderer-owned Z)")]
        [Tooltip("Additional world Z added on top of profile.grassZ. Keep small.")]
        [SerializeField] private float grassLayerZ = 0.0f;

        [Tooltip("Tiny epsilon added on top of (profile.grassZ + grassLayerZ). Use only to avoid z-fighting.")]
        [SerializeField] private float localZOffset = 0.0f;

        [Header("Coloring")]
        [Tooltip("If true, multiplies (tile base color) * (grass tile tint). Patch tint remains in shader via profile.")]
        [SerializeField] private bool applyPerTileTint = true;

        [Header("Debug")]
        [SerializeField] private bool logBind = true;
        [SerializeField] private bool logBuild = false;
        [SerializeField] private bool logDraw = false;

        // ----------------------------
        // Bound runtime dependencies
        // ----------------------------
        private WorldHost _host;
        private SparseChunkWorld _world;
        private ITileLibraryView _tileLibraryView;
        private TileLibrary _tileLibrary;
        private IChunkViewWindowProvider _view;

        // Runtime state
        private GrassTileChannel _grassChannel;

        private Material _runtimeMat;
        private MaterialPropertyBlock _mpb;

        private bool _bound;

        private readonly HashSet<ChunkCoord> _dirty = new();
        private readonly List<ChunkCoord> _dirtyScratch = new(512);
        private readonly Dictionary<ChunkCoord, ChunkCache> _cache = new();

        // view snapshot (polled from _view)
        private bool _hasView;
        private ChunkCoord _viewMin;
        private Vector2Int _viewSize;

        // Tile color getter (resolved once)
        private delegate bool TryGetTileColorFn(int tileId, out Color32 c);
        private TryGetTileColorFn _tryGetTileColor;

        private int ChunkSize => _world != null ? _world.ChunkSize : 0;
        private float CellSize => _host != null ? _host.CellSize : 1f;

        private Transform WorldRoot => (_host != null && _host.WorldRoot != null) ? _host.WorldRoot : transform;

        private float CombinedZ => (profile != null ? profile.grassZ : 0f) + grassLayerZ + localZOffset;

        // shader ids (match your shader file)
        private static readonly int ID_Color = Shader.PropertyToID("_Color");
        private static readonly int ID_UseXZPlane = Shader.PropertyToID("_UseXZPlane");

        private static readonly int ID_WindDir = Shader.PropertyToID("_WindDir");
        private static readonly int ID_WindAmp = Shader.PropertyToID("_WindAmp");
        private static readonly int ID_WindFreq = Shader.PropertyToID("_WindFreq");
        private static readonly int ID_WindWorldScale = Shader.PropertyToID("_WindWorldScale");

        private static readonly int ID_BaseStiffness = Shader.PropertyToID("_BaseStiffness");
        private static readonly int ID_BendExponent = Shader.PropertyToID("_BendExponent");
        private static readonly int ID_SwayStrength = Shader.PropertyToID("_SwayStrength");
        private static readonly int ID_TipBoost = Shader.PropertyToID("_TipBoost");

        private static readonly int ID_PatchColorA = Shader.PropertyToID("_PatchColorA");
        private static readonly int ID_PatchColorB = Shader.PropertyToID("_PatchColorB");
        private static readonly int ID_PatchScale = Shader.PropertyToID("_PatchScale");
        private static readonly int ID_PatchStrength = Shader.PropertyToID("_PatchStrength");

        private static readonly int ID_EmissionStrength = Shader.PropertyToID("_EmissionStrength");


        private static readonly int ID_InstanceColor = Shader.PropertyToID("_InstanceColor");
        private Vector4[] _instanceColorBuf; // fixed-size 1023

        private void Awake()
        {
            _mpb ??= new MaterialPropertyBlock();
            if (_instanceColorBuf == null || _instanceColorBuf.Length != 1023)
                _instanceColorBuf = new Vector4[1023];
        }

        // ---------------------------------------------------------------------
        // IRenderChunkChannel
        // ---------------------------------------------------------------------

        public void Bind(WorldHost host, SparseChunkWorld world, ITileLibraryView tiles, IChunkViewWindowProvider view)
        {
            if (_bound) return;

            _host = host;
            _world = world;
            _tileLibraryView = tiles;
            _tileLibrary = tiles != null ? tiles.Library : null;
            _view = view;

            if (_host == null || _world == null)
            {
                UnityEngine.Debug.LogError("[ChunkGrassRenderer] Bind failed: host/world null.", this);
                return;
            }

            if (_view == null)
            {
                UnityEngine.Debug.LogError("[ChunkGrassRenderer] Bind failed: view provider is null.", this);
                return;
            }

            if (profile == null)
            {
                UnityEngine.Debug.LogError("[ChunkGrassRenderer] Missing GrassRenderProfile.", this);
                return;
            }

            if (!profile.IsValid(out var reason))
            {
                UnityEngine.Debug.LogError($"[ChunkGrassRenderer] Invalid GrassRenderProfile: {reason}", this);
                return;
            }

            if (_tileLibrary == null)
            {
                UnityEngine.Debug.LogError("[ChunkGrassRenderer] Bind failed: ITileLibraryView.Library is null.", this);
                return;
            }

            // Build grass channel from compiled tile definitions
            int maxTileId = ComputeMaxTileId(_tileLibrary);
            _grassChannel = GrassTileChannel.BuildFrom(_tileLibrary, maxTileId);

            // Resolve best color API
            _tryGetTileColor = ResolveTileColorGetter(_tileLibrary);

            EnsureRuntimeMaterial();

            if (_runtimeMat == null || _runtimeMat.shader == null || !_runtimeMat.shader.isSupported)
            {
                UnityEngine.Debug.LogError($"[ChunkGrassRenderer] Runtime material invalid. shader={(_runtimeMat?.shader ? _runtimeMat.shader.name : "NULL")}", this);
                return;
            }

            BindWorldEvents();

            _bound = true;

            // Force initial view snapshot + build
            DetectViewWindowChangeAndMarkDirty();

            if (logBind)
            {
                UnityEngine.Debug.Log(
                    $"[ChunkGrassRenderer] Bound (channel). chunkSize={ChunkSize}, cellSize={CellSize}, mesh={profile.EffectiveMesh?.name}, " +
                    $"mat={_runtimeMat.name}, shader={_runtimeMat.shader.name}, combinedZ={CombinedZ}",
                    this);
            }
        }

        public void Tick()
        {
            if (!_bound) return;

            UploadGlobalsFromProfile();
            DetectViewWindowChangeAndMarkDirty();

            RebuildDirtyInView();
            DrawVisible();
        }

        public void Unbind()
        {
            if (!_bound) return;

            UnbindWorldEvents();

            _bound = false;

            _dirty.Clear();
            _dirtyScratch.Clear();

            // Note: if you ever pool matrices, release them here.
            _cache.Clear();

            _hasView = false;

            _host = null;
            _world = null;
            _tileLibraryView = null;
            _tileLibrary = null;
            _view = null;
            _grassChannel = null;
            _tryGetTileColor = null;

            if (_runtimeMat != null)
            {
                Destroy(_runtimeMat);
                _runtimeMat = null;
            }
        }

        private void OnDisable()
        {
            // Safety: orchestrator should call UnbindAll(), but this prevents leaks when components are disabled manually.
            if (_bound)
                Unbind();
        }

        // ---------------------------------------------------------------------
        // Material + Globals
        // ---------------------------------------------------------------------
        private static Color32 NormalizeTintPreserveValue(Color32 c)
        {
            float r = c.r / 255f;
            float g = c.g / 255f;
            float b = c.b / 255f;

            float max = Mathf.Max(0.0001f, Mathf.Max(r, Mathf.Max(g, b)));

            // Scale so the brightest channel becomes 1.0 (preserves "value"/brightness)
            r = Mathf.Clamp01(r / max);
            g = Mathf.Clamp01(g / max);
            b = Mathf.Clamp01(b / max);

            return new Color32(
                (byte)Mathf.RoundToInt(r * 255f),
                (byte)Mathf.RoundToInt(g * 255f),
                (byte)Mathf.RoundToInt(b * 255f),
                c.a
            );
        }


        private void EnsureRuntimeMaterial()
        {
            if (_runtimeMat != null) return;

            if (profile == null || profile.materialTemplate == null)
            {
                UnityEngine.Debug.LogError("[ChunkGrassRenderer] profile/materialTemplate is null.", this);
                return;
            }

            _runtimeMat = new Material(profile.materialTemplate)
            {
                name = $"{profile.materialTemplate.name} (Grass Runtime)"
            };

            _runtimeMat.enableInstancing = true;

            if (profile.renderQueueOverride >= 0)
                _runtimeMat.renderQueue = profile.renderQueueOverride;

            if (profile.mainTextureOverride != null)
            {
                if (_runtimeMat.HasProperty("_MainTex"))
                    _runtimeMat.SetTexture("_MainTex", profile.mainTextureOverride);
                if (_runtimeMat.HasProperty("_BaseMap"))
                    _runtimeMat.SetTexture("_BaseMap", profile.mainTextureOverride);
            }

            if (_runtimeMat.HasProperty(ID_UseXZPlane))
                _runtimeMat.SetFloat(ID_UseXZPlane, profile.useXZPlane);

            // Cutout safety clamp
            if (_runtimeMat.HasProperty("_Cutoff") && _runtimeMat.GetFloat("_Cutoff") <= 0f)
                _runtimeMat.SetFloat("_Cutoff", 0.001f);
            if (_runtimeMat.HasProperty("_AlphaCutoff") && _runtimeMat.GetFloat("_AlphaCutoff") <= 0f)
                _runtimeMat.SetFloat("_AlphaCutoff", 0.001f);

        }


        private void UploadGlobalsFromProfile()
        {
            // Use MPB for per-frame updates (wind etc.)
            _mpb.SetFloat(ID_UseXZPlane, profile.useXZPlane);

            _mpb.SetVector(ID_WindDir, profile.windDirection);
            _mpb.SetFloat(ID_WindAmp, profile.windAmplitude);
            _mpb.SetFloat(ID_WindFreq, profile.windFrequency);
            _mpb.SetFloat(ID_WindWorldScale, profile.windWorldScale);

            _mpb.SetFloat(ID_BaseStiffness, profile.baseStiffness);
            _mpb.SetFloat(ID_BendExponent, profile.bendExponent);
            _mpb.SetFloat(ID_SwayStrength, profile.swayStrength);
            _mpb.SetFloat(ID_TipBoost, profile.tipBoost);

            _mpb.SetColor(ID_PatchColorA, profile.patchColorA);
            _mpb.SetColor(ID_PatchColorB, profile.patchColorB);
            _mpb.SetFloat(ID_PatchScale, profile.patchScale);
            _mpb.SetFloat(ID_PatchStrength, profile.patchStrength);

            _mpb.SetFloat(ID_EmissionStrength, profile.emissionStrength);
        }

        // ---------------------------------------------------------------------
        // World events -> dirty
        // ---------------------------------------------------------------------

        private void BindWorldEvents()
        {
            _world.TileUpdated += OnTileUpdated;
            _world.ChunkCreated += OnChunkChanged;
            _world.ChunkRemoved += OnChunkChanged;
            _world.ChunkStorageKindChanged += OnChunkStorageChanged;
        }

        private void UnbindWorldEvents()
        {
            if (_world == null) return;

            _world.TileUpdated -= OnTileUpdated;
            _world.ChunkCreated -= OnChunkChanged;
            _world.ChunkRemoved -= OnChunkChanged;
            _world.ChunkStorageKindChanged -= OnChunkStorageChanged;
        }

        private void OnTileUpdated(SparseChunkWorld.TileUpdatedEvent e)
        {
            if (e.Result == SparseChunkWorld.TileUpdateResult.NoChange) return;
            _dirty.Add(e.Chunk);
        }

        private void OnChunkChanged(ChunkCoord cc)
        {
            _dirty.Add(cc);
            _cache.Remove(cc);
        }

        private void OnChunkStorageChanged(ChunkCoord cc, ChunkStorageKind oldKind, ChunkStorageKind newKind)
        {
            _dirty.Add(cc);
        }

        // ---------------------------------------------------------------------
        // View window (authoritative via IChunkViewWindowProvider)
        // ---------------------------------------------------------------------

        private void DetectViewWindowChangeAndMarkDirty()
        {
            if (!TryGetViewWindow(out var min, out var size))
                return;

            if (!_hasView || !_viewMin.Equals(min) || _viewSize != size)
            {
                _hasView = true;
                _viewMin = min;
                _viewSize = size;

                for (int y = 0; y < size.y; y++)
                    for (int x = 0; x < size.x; x++)
                        _dirty.Add(new ChunkCoord(min.X + x, min.Y + y));

                // Prune out-of-view cache entries
                var vmax = new ChunkCoord(min.X + size.x - 1, min.Y + size.y - 1);
                PruneCacheOutside(min, vmax);
            }
        }

        private bool TryGetViewWindow(out ChunkCoord min, out Vector2Int size)
        {
            min = default;
            size = default;

            if (_view == null)
                return false;

            // Interface in this project exposes ViewMin/ViewSize (no "Current" snapshot).
            min = _view.ViewMin;
            size = _view.ViewSize;

            return size.x > 0 && size.y > 0;
        }

        private void PruneCacheOutside(ChunkCoord vmin, ChunkCoord vmax)
        {
            if (_cache.Count == 0) return;

            // Collect keys to remove
            _dirtyScratch.Clear();
            foreach (var kv in _cache)
            {
                var cc = kv.Key;
                if (cc.X < vmin.X || cc.Y < vmin.Y || cc.X > vmax.X || cc.Y > vmax.Y)
                    _dirtyScratch.Add(cc);
            }

            for (int i = 0; i < _dirtyScratch.Count; i++)
                _cache.Remove(_dirtyScratch[i]);

            _dirtyScratch.Clear();
        }

        // ---------------------------------------------------------------------
        // Build / cache
        // ---------------------------------------------------------------------

        private void RebuildDirtyInView()
        {
            if (_dirty.Count == 0) return;
            if (!TryGetViewWindow(out var vmin, out var vsize)) return;

            var vmax = new ChunkCoord(vmin.X + vsize.x - 1, vmin.Y + vsize.y - 1);

            _dirtyScratch.Clear();
            foreach (var cc in _dirty) _dirtyScratch.Add(cc);
            _dirty.Clear();

            int rebuilt = 0;

            for (int i = 0; i < _dirtyScratch.Count; i++)
            {
                var cc = _dirtyScratch[i];

                if (cc.X < vmin.X || cc.Y < vmin.Y || cc.X > vmax.X || cc.Y > vmax.Y)
                    continue;

                BuildChunk(cc);
                rebuilt++;
            }

            if (logBuild && rebuilt > 0)
                UnityEngine.Debug.Log($"[ChunkGrassRenderer] Rebuilt {rebuilt} chunks. cache={_cache.Count}", this);

            _dirtyScratch.Clear();
        }
        [SerializeField] private bool logBuildDetails = false;
        [SerializeField] private int logBuildMaxClumps = 12;   // limit spam
        [SerializeField] private int logBuildMaxTiles = 12;    // limit spam

        private void BuildChunk(ChunkCoord cc)
        {
            if (_world == null)
            {
                _cache.Remove(cc);
                return;
            }

            if (!_world.TryGetChunk(cc, out Chunk chunk) || chunk == null)
            {
                if (logBuild)
                    UnityEngine.Debug.Log($"[ChunkGrassRenderer] Build {cc}: missing chunk -> remove cache", this);

                _cache.Remove(cc);
                return;
            }

            int cs = ChunkSize;
            float cell = Mathf.Max(0.0001f, CellSize);

            int maxClumps = Mathf.Max(0, profile.maxClumpsPerChunk);
            float padding = Mathf.Clamp01(profile.tilePadding);
            float z = CombinedZ;

            var batches = new Dictionary<int, Batch>(64);

            int grassTiles = 0;
            int totalClumps = 0;

            int dbgTilesLogged = 0;
            int dbgClumpsLogged = 0;

            for (int ly = 0; ly < cs; ly++)
                for (int lx = 0; lx < cs; lx++)
                {
                    int tileId = chunk.Get(lx, ly);

                    if (_grassChannel == null || !_grassChannel.IsGrassable(tileId))
                        continue;

                    var info = _grassChannel.Get(tileId);
                    if (!info.grassable)
                        continue;

                    grassTiles++;

                    float target =
                        Mathf.Max(0f, profile.clumpsPerTile) *
                        Mathf.Max(0.0001f, profile.clumpsPerTileMultiplier) *
                        Mathf.Max(0f, info.density);

                    if (target <= 0f)
                        continue;

                    int baseCount = Mathf.FloorToInt(target);
                    float frac = target - baseCount;

                    int count = baseCount;
                    if (frac > 0f)
                    {
                        float r = Hash01(cc.X, cc.Y, lx, ly, 777, profile.globalSeed);
                        if (r < frac) count++;
                    }

                    if (count <= 0)
                        continue;

                    // Final color comes from assets:
                    // - tile base color from TileLibrary
                    // - grass tint from GrassTileChannel
                    Color32 tileC = Color.white;
                    bool hasTileC = _tryGetTileColor != null && _tryGetTileColor(tileId, out tileC);
                    if (!hasTileC)
                        tileC = new Color32(255, 255, 255, 255);

                    Color32 grassTint = info.tint;

                    // Multiply tile color by grass tint (both asset-defined)
                    Color32 tileTint = NormalizeTintPreserveValue(tileC);

                    Color32 final32 = applyPerTileTint
                        ? MultiplyColor32(tileTint, grassTint)
                        : grassTint;


                    int colorKey =
                        (final32.r << 24) |
                        (final32.g << 16) |
                        (final32.b << 8) |
                         final32.a;

                    if (!batches.TryGetValue(colorKey, out var batch))
                    {
                        batch = new Batch(final32);
                        batches[colorKey] = batch;
                    }

                    if (logBuildDetails && dbgTilesLogged < logBuildMaxTiles)
                    {
                        UnityEngine.Debug.Log(
                            $"[GrassColorBuild] cc={cc} lx={lx} ly={ly} tileId={tileId} " +
                            $"hasTileC={hasTileC} tileC=({tileC.r},{tileC.g},{tileC.b},{tileC.a}) " +
                            $"grassTint=({grassTint.r},{grassTint.g},{grassTint.b},{grassTint.a}) " +
                            $"final=({final32.r},{final32.g},{final32.b},{final32.a}) " +
                            $"applyPerTileTint={applyPerTileTint}",
                            this);
                    }

                    // Tile center in grid space
                    float baseX = (cc.X * cs + lx + 0.5f) * cell;
                    float baseY = (cc.Y * cs + ly + 0.5f) * cell;

                    if (logBuildDetails && dbgTilesLogged < logBuildMaxTiles)
                    {
                        dbgTilesLogged++;
                        UnityEngine.Debug.Log(
                            $"[ChunkGrassRenderer] BuildTile cc={cc} lx={lx} ly={ly} tileId={tileId} " +
                            $"density={info.density:0.###} target={target:0.###} count={count} baseX={baseX:0.###} baseY={baseY:0.###} cell={cell:0.###} z={z:0.###}",
                            this);
                    }

                    for (int i = 0; i < count; i++)
                    {
                        if (totalClumps >= maxClumps)
                            break;
                        float h1 = Hash01(cc.X, cc.Y, lx, ly, i * 2 + 1, profile.globalSeed);
                        float h2 = Hash01(cc.X, cc.Y, lx, ly, i * 2 + 2, profile.globalSeed);

                        //if (logBuildDetails && dbgClumpsLogged < logBuildMaxClumps)
                        //{
                        //    UnityEngine.Debug.Log($"[ChunkGrassRenderer] Hash cc={cc} lx={lx} ly={ly} i={i} h1={h1} h2={h2}", this);
                        //}

                        // Jitter around center, constrained by padding
                        float jx = Mathf.Lerp(-0.5f + padding, 0.5f - padding,
                            Hash01(cc.X, cc.Y, lx, ly, i * 2 + 1, profile.globalSeed));
                        float jy = Mathf.Lerp(-0.5f + padding, 0.5f - padding,
                            Hash01(cc.X, cc.Y, lx, ly, i * 2 + 2, profile.globalSeed));

                        float wx = baseX + jx * cell;
                        float wy = baseY + jy * cell;

                        Vector3 posLocal = new Vector3(wx, wy, z);
                        Vector3 pos = (WorldRoot != null) ? WorldRoot.TransformPoint(posLocal) : posLocal;

                        float rotDeg = profile.baseRotationDeg;
                        if (profile.randomRotation)
                            rotDeg += Mathf.Lerp(profile.minRotationDeg, profile.maxRotationDeg,
                                Hash01(cc.X, cc.Y, lx, ly, i + 17, profile.globalSeed));

                        Quaternion rot = (profile.useXZPlane >= 0.5f)
                            ? Quaternion.Euler(0f, rotDeg, 0f)
                            : Quaternion.Euler(0f, 0f, rotDeg);

                        float sx = Mathf.Lerp(profile.scaleWidthMin, profile.scaleWidthMax,
                            Hash01(cc.X, cc.Y, lx, ly, i + 53, profile.globalSeed));

                        float sy = Mathf.Lerp(profile.scaleHeightMin, profile.scaleHeightMax,
                            Hash01(cc.X, cc.Y, lx, ly, i + 61, profile.globalSeed)) * Mathf.Max(0.0001f, profile.heightBias);

                        // Prevent collapse
                        sx = Mathf.Max(0.0001f, sx);
                        sy = Mathf.Max(0.0001f, sy);

                        if (profile.uniformScale)
                        {
                            float u = Mathf.Min(sx, sy);
                            sx = u; sy = u;
                        }

                        batch.Matrices.Add(Matrix4x4.TRS(pos, rot, new Vector3(sx, sy, 1f)));
                        totalClumps++;

                        if (logBuildDetails && dbgClumpsLogged < logBuildMaxClumps)
                        {
                            dbgClumpsLogged++;
                            UnityEngine.Debug.Log(
                                $"[ChunkGrassRenderer] BuildClump cc={cc} lx={lx} ly={ly} i={i} " +
                                $"jx={jx:0.###} jy={jy:0.###} wx={wx:0.###} wy={wy:0.###} pos={pos} sx={sx:0.###} sy={sy:0.###}",
                                this);
                        }
                    }

                    if (totalClumps >= maxClumps)
                        break;
                }

            if (batches.Count == 0 || totalClumps == 0)
            {
                if (logBuild)
                    UnityEngine.Debug.Log($"[ChunkGrassRenderer] Build {cc}: grassTiles={grassTiles} clumps={totalClumps} -> remove cache", this);

                _cache.Remove(cc);
                return;
            }

            _cache[cc] = new ChunkCache(batches);

            if (logBuild)
                UnityEngine.Debug.Log($"[ChunkGrassRenderer] Build {cc}: grassTiles={grassTiles}, clumps={totalClumps}, batches={batches.Count}, z={z:0.###}", this);
        }

        private static Color32 MultiplyColor32(Color32 a, Color32 b)
        {
            // Convert bytes -> Colors (0..1)
            Color ca = new Color(a.r / 255f, a.g / 255f, a.b / 255f, a.a / 255f);
            Color cb = new Color(b.r / 255f, b.g / 255f, b.b / 255f, b.a / 255f);

            // If project is Linear, do the multiply in linear space, then convert back to gamma.
            // If project is Gamma, multiply directly.
            Color outC;
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                outC = (ca.linear * cb.linear);
                outC = outC.gamma;
            }
            else
            {
                outC = ca * cb;
            }

            // Clamp + back to Color32
            outC.r = Mathf.Clamp01(outC.r);
            outC.g = Mathf.Clamp01(outC.g);
            outC.b = Mathf.Clamp01(outC.b);
            outC.a = Mathf.Clamp01(outC.a);

            return (Color32)outC;
        }



        // ---------------------------------------------------------------------
        // Draw
        // ---------------------------------------------------------------------
        [SerializeField] private bool logDrawMatrices = true;
        [SerializeField] private bool debugForceSpread = false;     // keep OFF unless debugging
        [SerializeField] private float debugSpreadStep = 0.5f;
        [SerializeField] private bool debugDrawNonInstanced = false; // keep OFF unless debugging
        [SerializeField] private int debugMaxMatrixLogsPerFrame = 6;

        // Debug controls (put these as fields if you want them in Inspector)
        [SerializeField] private int debugMpbMode = 1;
        // 0 = null MPB (baseline)
        // 1 = color-only MPB (sets only _Color/_BaseColor)
        // 2 = full MPB (_mpb as currently used)

        [SerializeField] private bool debugLogMpbMode = true;
        [SerializeField] private int debugMaxLogsPerFrame = 6;

        // Fixed-size buffers (Unity MPB caches array size — keep it constant)
        private readonly Matrix4x4[] _matBuf = new Matrix4x4[1023];
        private readonly Vector4[] _colBuf = new Vector4[1023];

        private void DrawVisible()
        {
            if (_cache.Count == 0) return;
            if (_runtimeMat == null) return;
            if (!TryGetViewWindow(out var vmin, out var vsize)) return;

            Mesh mesh = profile.EffectiveMesh;
            if (mesh == null) return;

            // MPB must exist and already contain your per-frame globals (wind/patch/etc.)
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            var vmax = new ChunkCoord(vmin.X + vsize.x - 1, vmin.Y + vsize.y - 1);
            var shadowMode = profile.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            int drawCalls = 0;
            int instances = 0;

            for (int cy = vmin.Y; cy <= vmax.Y; cy++)
                for (int cx = vmin.X; cx <= vmax.X; cx++)
                {
                    var cc = new ChunkCoord(cx, cy);
                    if (!_cache.TryGetValue(cc, out var cache))
                        continue;

                    foreach (var kv in cache.Batches)
                    {
                        var batch = kv.Value;
                        int total = batch.Matrices.Count;
                        if (total == 0) continue;

                        // This color was computed in BuildChunk:
                        // - applyPerTileTint? tileTint*grassTint : grassTint
                        var c32 = batch.Color;
                        Vector4 instColor = new Vector4(
                            c32.r / 255f,
                            c32.g / 255f,
                            c32.b / 255f,
                            c32.a / 255f
                        );

                        int offset = 0;
                        while (offset < total)
                        {
                            int count = Mathf.Min(1023, total - offset);

                            // Fill matrix buffer slice
                            for (int i = 0; i < count; i++)
                                _matBuf[i] = batch.Matrices[offset + i];

                            // Fill color buffer slice
                            for (int i = 0; i < count; i++)
                                _colBuf[i] = instColor;

                            // CRITICAL: always set the SAME array size (1023) to avoid Unity “cap to previous size”
                            _mpb.SetVectorArray(ID_InstanceColor, _colBuf);

                            Graphics.DrawMeshInstanced(
                                mesh,
                                0,
                                _runtimeMat,
                                _matBuf,
                                count,
                                _mpb,
                                shadowMode,
                                profile.receiveShadows,
                                gameObject.layer
                            );

                            drawCalls++;
                            instances += count;
                            offset += count;
                        }
                    }
                }

            if (logDraw && drawCalls > 0)
                UnityEngine.Debug.Log($"[ChunkGrassRenderer] Draw: calls={drawCalls}, instances={instances}, cache={_cache.Count}", this);
        }


        // ---------------------------------------------------------------------
        // Internal cache types
        // ---------------------------------------------------------------------

        private sealed class ChunkCache
        {
            public readonly Dictionary<int, Batch> Batches;
            public ChunkCache(Dictionary<int, Batch> batches) => Batches = batches;
        }

        private sealed class Batch
        {
            public readonly Color32 Color;
            public readonly List<Matrix4x4> Matrices = new(256);
            public Batch(Color32 c) => Color = c;
        }

        private static class MatrixArrayPool
        {
            private static readonly Dictionary<int, Matrix4x4[]> Pool = new();
            public static Matrix4x4[] Get(int size)
            {
                if (!Pool.TryGetValue(size, out var arr) || arr == null || arr.Length != size)
                {
                    arr = new Matrix4x4[size];
                    Pool[size] = arr;
                }
                return arr;
            }
        }

        // ---------------------------------------------------------------------
        // Tile helpers
        // ---------------------------------------------------------------------

        private static int ComputeMaxTileId(TileLibrary lib)
        {
            int max = 0;
            foreach (var def in lib.EnumerateDefs())
            {
                if (def != null && def.TileId > max)
                    max = def.TileId;
            }
            return max;
        }

        private static TryGetTileColorFn ResolveTileColorGetter(TileLibrary lib)
        {
            // Prefer: TryGetBaseColor(int, out Color32) if present in your newer TileLibrary
            var miBase = typeof(TileLibrary).GetMethod("TryGetBaseColor", new[] { typeof(int), typeof(Color32).MakeByRefType() });
            if (miBase != null)
            {
                return (int tileId, out Color32 c) =>
                {
                    object[] args = { tileId, default(Color32) };
                    bool ok = (bool)miBase.Invoke(lib, args);
                    c = (Color32)args[1];
                    return ok;
                };
            }

            // Fallback: TryGetColor(int, out Color32)
            var miColor = typeof(TileLibrary).GetMethod("TryGetColor", new[] { typeof(int), typeof(Color32).MakeByRefType() });
            if (miColor != null)
            {
                return (int tileId, out Color32 c) =>
                {
                    object[] args = { tileId, default(Color32) };
                    bool ok = (bool)miColor.Invoke(lib, args);
                    c = (Color32)args[1];
                    return ok;
                };
            }

            // No color API found
            return (int _, out Color32 c) =>
            {
                c = new Color32(255, 255, 255, 255);
                return false;
            };
        }

        private static float Hash01(int a, int b, int c, int d, int e, int f)
        {
            unchecked
            {
                // FNV-1a mix over all 6 ints
                uint h = 2166136261u;

                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h = (h ^ (uint)c) * 16777619u;
                h = (h ^ (uint)d) * 16777619u;
                h = (h ^ (uint)e) * 16777619u;
                h = (h ^ (uint)f) * 16777619u;

                // extra avalanche
                h ^= h >> 13;
                h *= 1274126177u;
                h ^= h >> 16;

                // [0,1)
                return (h & 0x00FFFFFFu) / 16777216f;
            }
        }

    }
}
