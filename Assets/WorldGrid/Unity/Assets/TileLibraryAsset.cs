using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGrid.Runtime.Tiles;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WorldGrid.Unity.Assets
{
    [CreateAssetMenu(menuName = "WorldGrid/Tile Library Asset", fileName = "TileLibraryAsset")]
    public sealed class TileLibraryAsset : ScriptableObject
    {
        // ---------------------------
        // Atlas / Material (authoring)
        // ---------------------------

        [Header("Atlas")]
        [Tooltip("The atlas/spritesheet texture (PNG). Optional if using procedural backends or manual layout authoring.")]
        public Texture2D atlasTexture;

        [Tooltip("Optional: material that uses the atlasTexture. If null, it can be auto-created in editor.")]
        public Material atlasMaterial;

        [Header("Atlas Material Auto-Create (Editor)")]
        [Tooltip("If true, the asset will auto-create/update atlasMaterial in the Editor when possible.")]
        public bool autoCreateAtlasMaterial = true;

        [Tooltip("Material asset name suffix (created next to this TileLibraryAsset).")]
        public string autoMaterialSuffix = "_WorldGrid_Mat";

        [Tooltip(
            "Preferred shader name for the auto-created material.\n" +
            "Built-in pipeline default: 'WorldGrid/Unlit Vertex Tint Blend' (recommended for tint+blend).\n" +
            "Other examples: 'Sprites/Default', 'Unlit/Transparent', 'Unlit/Texture'.")]
        public string autoMaterialShaderName = "WorldGrid/Unlit Vertex Tint Blend";

        // ---------------------------
        // Atlas layout intent (semantic)
        // ---------------------------

        public enum AtlasLayoutMode
        {
            FromTexture,    // derive atlas pixel size from atlasTexture.width/height
            ManualPixels,   // explicit atlas pixel size (width/height in pixels)
            ManualGrid      // explicit grid (columns/rows or tile count), atlas pixels derived from tile size + padding
        }

        [Header("Atlas Layout")]
        [Tooltip("How the atlas layout is interpreted when computing tile coords/UVs and auto-populating entries.")]
        public AtlasLayoutMode atlasLayoutMode = AtlasLayoutMode.FromTexture;

        [Tooltip("Tile size in pixels (e.g., 16x16, 32x32).")]
        public Vector2Int tilePixelSize = new Vector2Int(32, 32);

        [Tooltip("Optional padding/gutter between tiles in pixels (0 for tightly packed atlases).")]
        public Vector2Int paddingPixels = Vector2Int.zero;

        [Tooltip(
            "If true, tile coords are authored with (0,0) at the TOP-LEFT of the atlas grid.\n" +
            "If false, (0,0) is BOTTOM-LEFT (matches UV space).")]
        public bool originTopLeft = true;

        [Header("Manual Atlas Layout (No Texture Required)")]
        [Tooltip("Used when atlasLayoutMode = ManualPixels.")]
        public int manualAtlasWidthPixels = 1024;

        [Tooltip("Used when atlasLayoutMode = ManualPixels.")]
        public int manualAtlasHeightPixels = 1024;

        [Tooltip("Used when atlasLayoutMode = ManualGrid. Grid columns for auto-populate and UV coordinate layout.")]
        public int manualColumns = 8;

        [Tooltip("Used when atlasLayoutMode = ManualGrid if inferRowsFromTileCount is false.")]
        public int manualRows = 8;

        [Tooltip("Used when atlasLayoutMode = ManualGrid. If true, rows are computed from tile count.")]
        public bool inferRowsFromTileCount = true;

        [Tooltip("Used when atlasLayoutMode = ManualGrid. Total tiles to auto-populate (entries will be created/trimmed to this).")]
        public int manualTileCount = 64;

        // ---------------------------
        // Auto-populate / ids
        // ---------------------------

        [Header("Auto-Populate")]
        [Tooltip("If true, auto-populate will reserve tileId=0 for 'empty/default' and start real tiles at 1.")]
        public bool reserveZeroForEmpty = true;

        [Tooltip("If reserveZeroForEmpty is false, auto-populate will start from this id. If true, it starts at 1.")]
        public int autoIdStart = 0;

        // ---------------------------
        // Tile definitions (semantic)
        // ---------------------------

        [Header("Tile Definitions")]
        public List<Entry> entries = new();

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("The integer tileId stored in WorldGrid.")]
            public int tileId;

            [Tooltip("Human-readable name for debugging/tools (optional during auto-populate).")]
            public string name;

            [Tooltip("Tile coordinate in the atlas grid (x=column, y=row). Origin depends on 'originTopLeft'.")]
            public Vector2Int tileCoord = Vector2Int.zero;

            [Tooltip("Size in tiles. Leave as (1,1) for a standard single tile.")]
            public Vector2Int tileSpan = Vector2Int.one;

            [Header("Visual Semantics")]
            [Tooltip("Semantic base color for this tile (used by procedural color atlases and optional sprite tinting).")]
            public Color32 color = new Color32(255, 255, 255, 255);

            [Tooltip("Optional per-cell brightness jitter amplitude for this tile (0..0.25 recommended). Used by renderer tinting.")]
            [Range(0f, 0.25f)]
            public float colorJitter = 0.05f;

            [Tooltip("How much to blend tint into the sprite. 0 = no tint, 1 = full tint.")]
            [Range(0f, 1f)]
            public float colorBlend = 1f;

            [Header("Tags (Optional)")]
            [Tooltip("Optional tags for debugging/querying (string-based for now).")]
            public List<string> tags = new();

            [Header("Properties (Optional)")]
            [Tooltip("Optional, extensible semantics (authoring/build time).")]
            public List<TileProperty> properties = new();

            [Header("Advanced")]
            [Tooltip("If enabled, uvMin/uvMax are used directly instead of computing from tileCoord/tileSpan.")]
            public bool overrideUv = false;

            [Tooltip("Normalized UV min (0..1), bottom-left.")]
            public Vector2 uvMin;

            [Tooltip("Normalized UV max (0..1), top-right.")]
            public Vector2 uvMax;
        }

        // ---------------------------
        // Effective atlas sizing helpers
        // ---------------------------

        public bool TryGetEffectiveAtlasSize(out int widthPx, out int heightPx)
        {
            switch (atlasLayoutMode)
            {
                case AtlasLayoutMode.FromTexture:
                    if (atlasTexture == null)
                    {
                        widthPx = 0;
                        heightPx = 0;
                        return false;
                    }
                    widthPx = atlasTexture.width;
                    heightPx = atlasTexture.height;
                    return widthPx > 0 && heightPx > 0;

                case AtlasLayoutMode.ManualPixels:
                    widthPx = Mathf.Max(1, manualAtlasWidthPixels);
                    heightPx = Mathf.Max(1, manualAtlasHeightPixels);
                    return true;

                case AtlasLayoutMode.ManualGrid:
                    {
                        int cols = Mathf.Max(1, manualColumns);
                        int rows = Mathf.Max(1, inferRowsFromTileCount
                            ? Mathf.CeilToInt(Mathf.Max(1, manualTileCount) / (float)cols)
                            : Mathf.Max(1, manualRows));

                        int stepX = Mathf.Max(1, tilePixelSize.x + Mathf.Max(0, paddingPixels.x));
                        int stepY = Mathf.Max(1, tilePixelSize.y + Mathf.Max(0, paddingPixels.y));

                        widthPx = cols * stepX;
                        heightPx = rows * stepY;
                        return true;
                    }

                default:
                    widthPx = 0;
                    heightPx = 0;
                    return false;
            }
        }

        public bool TryGetEffectiveGridSize(out int tilesX, out int tilesY)
        {
            tilesX = 0;
            tilesY = 0;

            if (!TryGetEffectiveAtlasSize(out int atlasW, out int atlasH))
                return false;

            int stepX = tilePixelSize.x + paddingPixels.x;
            int stepY = tilePixelSize.y + paddingPixels.y;

            if (stepX <= 0 || stepY <= 0)
                return false;

            tilesX = atlasW / stepX;
            tilesY = atlasH / stepY;

            if (tilesX <= 0 || tilesY <= 0)
                return false;

            return true;
        }

        // ---------------------------
        // Validation
        // ---------------------------

        public void ValidateOrThrow()
        {
            if (tilePixelSize.x <= 0 || tilePixelSize.y <= 0)
                throw new ArgumentOutOfRangeException(nameof(tilePixelSize), "Tile size must be > 0 in both dimensions.");

            if (paddingPixels.x < 0 || paddingPixels.y < 0)
                throw new ArgumentOutOfRangeException(nameof(paddingPixels), "Padding cannot be negative.");

            if (atlasLayoutMode == AtlasLayoutMode.ManualPixels)
            {
                if (manualAtlasWidthPixels <= 0 || manualAtlasHeightPixels <= 0)
                    throw new ArgumentOutOfRangeException(nameof(manualAtlasWidthPixels), "Manual atlas pixel size must be > 0.");
            }

            if (atlasLayoutMode == AtlasLayoutMode.ManualGrid)
            {
                if (manualColumns <= 0)
                    throw new ArgumentOutOfRangeException(nameof(manualColumns), "Manual columns must be > 0.");
                if (!inferRowsFromTileCount && manualRows <= 0)
                    throw new ArgumentOutOfRangeException(nameof(manualRows), "Manual rows must be > 0 when not inferring rows.");
                if (manualTileCount <= 0)
                    throw new ArgumentOutOfRangeException(nameof(manualTileCount), "Manual tile count must be > 0.");
            }

            var ids = new HashSet<int>();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null) continue;

                if (!ids.Add(e.tileId))
                    throw new InvalidOperationException($"{name}: Duplicate tileId found: {e.tileId}.");

                if (e.tileSpan.x <= 0 || e.tileSpan.y <= 0)
                    throw new ArgumentOutOfRangeException(nameof(Entry.tileSpan), $"{name}: tileSpan must be >= (1,1).");
            }
        }

        // ---------------------------
        // Runtime build (legacy convenience)
        // ---------------------------

        public TileLibrary BuildRuntime()
        {
            ValidateOrThrow();

#if UNITY_EDITOR
            //if (autoCreateAtlasMaterial)
            //    EnsureAtlasMaterialExistsOrUpdated();
#endif

            if (!TryGetEffectiveAtlasSize(out int atlasW, out int atlasH))
                throw new InvalidOperationException(
                    $"{name}: Cannot BuildRuntime() because atlasLayoutMode={atlasLayoutMode} has no effective atlas size. " +
                    $"Assign an atlasTexture or use ManualPixels/ManualGrid.");

            var lib = new TileLibrary();

            foreach (var e in entries)
            {
                if (e == null) continue;

                string entryName = string.IsNullOrWhiteSpace(e.name) ? $"Tile {e.tileId}" : e.name;

                RectUv uv = e.overrideUv
                    ? new RectUv(e.uvMin.x, e.uvMin.y, e.uvMax.x, e.uvMax.y)
                    : ComputeUvFromTileCoord(
                        atlasW,
                        atlasH,
                        tilePixelSize,
                        paddingPixels,
                        originTopLeft,
                        e.tileCoord,
                        e.tileSpan);

                // Do NOT mutate e.properties (asset data). Build a runtime list and inject TileColorProperty.
                List<TileProperty> runtimeProps;

                if (e.properties != null && e.properties.Count > 0)
                {
                    runtimeProps = new List<TileProperty>(e.properties.Count + 1);
                    runtimeProps.AddRange(e.properties);
                }
                else
                {
                    runtimeProps = new List<TileProperty>(1);
                }

                runtimeProps.Add(new TileColorProperty(e.color, e.colorJitter, e.colorBlend));

                var def = new TileDef(
                    e.tileId,
                    entryName,
                    uv,
                    e.tags,
                    runtimeProps);

                lib.Set(def);
            }

            return lib;
        }

        // ---------------------------
        // UV computation
        // ---------------------------

        public static RectUv ComputeUvFromTileCoord(
            int atlasWidthPx,
            int atlasHeightPx,
            Vector2Int tileSizePx,
            Vector2Int paddingPx,
            bool originTopLeft,
            Vector2Int tileCoord,
            Vector2Int tileSpan
        )
        {
            if (atlasWidthPx <= 0) throw new ArgumentOutOfRangeException(nameof(atlasWidthPx));
            if (atlasHeightPx <= 0) throw new ArgumentOutOfRangeException(nameof(atlasHeightPx));
            if (tileSizePx.x <= 0 || tileSizePx.y <= 0)
                throw new ArgumentOutOfRangeException(nameof(tileSizePx), "Tile size must be > 0.");
            if (paddingPx.x < 0 || paddingPx.y < 0)
                throw new ArgumentOutOfRangeException(nameof(paddingPx), "Padding cannot be negative.");
            if (tileSpan.x <= 0 || tileSpan.y <= 0)
                throw new ArgumentOutOfRangeException(nameof(tileSpan), "Tile span must be >= (1,1).");

            int stepX = tileSizePx.x + paddingPx.x;
            int stepY = tileSizePx.y + paddingPx.y;

            int x0 = tileCoord.x * stepX;
            int y0 = tileCoord.y * stepY;

            if (originTopLeft)
                y0 = atlasHeightPx - y0 - tileSizePx.y;

            int w = tileSpan.x * tileSizePx.x + (tileSpan.x - 1) * paddingPx.x;
            int h = tileSpan.y * tileSizePx.y + (tileSpan.y - 1) * paddingPx.y;

            float uMin = (float)x0 / atlasWidthPx;
            float vMin = (float)y0 / atlasHeightPx;
            float uMax = (float)(x0 + w) / atlasWidthPx;
            float vMax = (float)(y0 + h) / atlasHeightPx;

            return new RectUv(uMin, vMin, uMax, vMax);
        }

        // ---------------------------
        // Editor convenience
        // ---------------------------

        [ContextMenu("Clear Entries")]
        public void ClearEntries()
        {
            entries.Clear();
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        [ContextMenu("Auto-Populate Entries From Atlas Layout")]
        public void AutoPopulateEntriesFromAtlasLayout()
        {
            if (tilePixelSize.x <= 0 || tilePixelSize.y <= 0)
            {
                UnityEngine.Debug.LogError($"{name}: tilePixelSize must be > 0.", this);
                return;
            }

            if (paddingPixels.x < 0 || paddingPixels.y < 0)
            {
                UnityEngine.Debug.LogError($"{name}: paddingPixels cannot be negative.", this);
                return;
            }

            int tilesX;
            int tilesY;

            if (atlasLayoutMode == AtlasLayoutMode.ManualGrid)
            {
                tilesX = Mathf.Max(1, manualColumns);
                tilesY = Mathf.Max(1, inferRowsFromTileCount
                    ? Mathf.CeilToInt(Mathf.Max(1, manualTileCount) / (float)tilesX)
                    : Mathf.Max(1, manualRows));
            }
            else
            {
                if (!TryGetEffectiveGridSize(out tilesX, out tilesY))
                {
                    UnityEngine.Debug.LogError(
                        $"{name}: Cannot auto-populate because no effective atlas/grid size is available. " +
                        $"Assign atlasTexture (FromTexture) or use ManualPixels/ManualGrid.",
                        this);
                    return;
                }
            }

            int capacity = tilesX * tilesY;

            int desiredCount;
            if (atlasLayoutMode == AtlasLayoutMode.ManualGrid)
                desiredCount = Mathf.Clamp(manualTileCount, 1, capacity);
            else
                desiredCount = capacity;

            entries.Clear();

            int nextId = reserveZeroForEmpty ? 1 : autoIdStart;
            int created = 0;

            for (int i = 0; i < desiredCount; i++)
            {
                int x = i % tilesX;
                int y = i / tilesX;

                var e = new Entry
                {
                    tileId = nextId++,
                    name = $"Tile {created}",
                    tileCoord = new Vector2Int(x, y),
                    tileSpan = Vector2Int.one,
                    tags = new List<string>(),
                    properties = new List<TileProperty>(),
                    overrideUv = false,
                    uvMin = Vector2.zero,
                    uvMax = Vector2.zero,
                    color = new Color32(255, 255, 255, 255),
                    colorJitter = 0.05f,
                    colorBlend = 1f
                };

                entries.Add(e);
                created++;
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif

            UnityEngine.Debug.Log($"{name}: Auto-populated {created} entries (grid {tilesX}x{tilesY}, mode={atlasLayoutMode}).", this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (tilePixelSize.x < 1) tilePixelSize.x = 1;
            if (tilePixelSize.y < 1) tilePixelSize.y = 1;

            if (paddingPixels.x < 0) paddingPixels.x = 0;
            if (paddingPixels.y < 0) paddingPixels.y = 0;

            if (manualAtlasWidthPixels < 1) manualAtlasWidthPixels = 1;
            if (manualAtlasHeightPixels < 1) manualAtlasHeightPixels = 1;

            if (manualColumns < 1) manualColumns = 1;
            if (manualRows < 1) manualRows = 1;
            if (manualTileCount < 1) manualTileCount = 1;

            if (autoIdStart < 0) autoIdStart = 0;

            if (autoCreateAtlasMaterial && atlasTexture != null)
            {
                //EnsureAtlasMaterialExistsOrUpdated();
            }
        }

        private void EnsureAtlasMaterialExistsOrUpdated()
        {
            if (atlasTexture == null)
                return;

            // Prefer configured shader; fallback to sensible Built-in options.
            Shader shader = Shader.Find(autoMaterialShaderName);
            if (shader == null)
            {
                shader =
                    Shader.Find("WorldGrid/Unlit Vertex Tint Blend") ??
                    Shader.Find("Sprites/Default") ??
                    Shader.Find("Unlit/Transparent") ??
                    Shader.Find("Unlit/Texture");
            }

            if (shader == null)
            {
                UnityEngine.Debug.LogWarning($"{name}: Could not find a usable shader for atlas material creation.", this);
                return;
            }

            // If atlasMaterial exists, update it in-place (shader + texture bindings).
            if (atlasMaterial != null)
            {
                if (atlasMaterial.shader != shader)
                    atlasMaterial.shader = shader;

                // Bind both names for compatibility (TileMaterialFactory + shader variants).
                if (atlasMaterial.HasProperty("_BaseMap"))
                    atlasMaterial.SetTexture("_BaseMap", atlasTexture);
                if (atlasMaterial.HasProperty("_MainTex"))
                    atlasMaterial.SetTexture("_MainTex", atlasTexture);

                EditorUtility.SetDirty(atlasMaterial);
                return;
            }

            // Create a new material asset next to this TileLibraryAsset.
            string assetPath = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(assetPath))
                return;

            string folder = System.IO.Path.GetDirectoryName(assetPath);
            string atlasName = atlasTexture != null ? atlasTexture.name : "Atlas";
            string assetName = name;
            string matName = $"{atlasName}__{assetName}{autoMaterialSuffix}.mat";
            string matPath = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.Combine(folder, matName));

            var mat = new Material(shader);

            // Bind both names for compatibility (TileMaterialFactory + shader variants).
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", atlasTexture);
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", atlasTexture);

            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();

            atlasMaterial = mat;
            EditorUtility.SetDirty(this);

            UnityEngine.Debug.Log($"{name}: Created atlasMaterial at '{matPath}' using shader '{shader.name}'.", this);
        }

        [ContextMenu("Compute UVs For Entries (Store Into uvMin/uvMax)")]
        public void ComputeAndStoreUvsForEntries()
        {
            ValidateOrThrow();

            if (!TryGetEffectiveAtlasSize(out int atlasW, out int atlasH))
            {
                UnityEngine.Debug.LogError(
                    $"{name}: Cannot compute UVs because atlasLayoutMode={atlasLayoutMode} has no effective atlas size. " +
                    $"Assign atlasTexture or use ManualPixels/ManualGrid.",
                    this);
                return;
            }

            int updated = 0;

            foreach (var e in entries)
            {
                if (e == null) continue;

                if (e.overrideUv)
                    continue;

                RectUv uv = ComputeUvFromTileCoord(
                    atlasW,
                    atlasH,
                    tilePixelSize,
                    paddingPixels,
                    originTopLeft,
                    e.tileCoord,
                    e.tileSpan);

                e.uvMin = new Vector2(uv.UMin, uv.VMin);
                e.uvMax = new Vector2(uv.UMax, uv.VMax);
                updated++;
            }

            EditorUtility.SetDirty(this);
            UnityEngine.Debug.Log($"{name}: Computed UVs for {updated} entries (stored into uvMin/uvMax for visibility).", this);
        }
#endif
    }
}
