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
        #region Atlas / Material (Authoring)

        [Header("Atlas")]
        [Tooltip("The atlas/spritesheet texture (PNG). Optional if using manual layout authoring.")]
        public Texture2D atlasTexture;

        [Tooltip("Optional: material that uses the atlasTexture. If null, it can be created/updated in the Editor.")]
        public Material atlasMaterial;

        [Header("Atlas Material Auto-Create (Editor)")]
        [Tooltip("If true, the asset can auto-create/update atlasMaterial in the Editor when possible.")]
        public bool autoCreateAtlasMaterial = true;

        [Tooltip("Material asset name suffix (created next to this TileLibraryAsset).")]
        public string autoMaterialSuffix = "_WorldGrid_Mat";

        [Tooltip(
            "Preferred shader name for the auto-created material.\n" +
            "Examples: 'WorldGrid/Unlit Vertex Tint Blend', 'Sprites/Default', 'Unlit/Transparent', 'Unlit/Texture'.")]
        public string autoMaterialShaderName = "WorldGrid/Unlit Vertex Tint Blend";

        #endregion

        #region Atlas Layout

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
            "If true, tile coords are authored with (0,0) at the top-left of the atlas grid.\n" +
            "If false, (0,0) is bottom-left (matches UV space).")]
        public bool originTopLeft = true;

        [Header("Manual Atlas Layout (No Texture Required)")]
        [Tooltip("Used when atlasLayoutMode = ManualPixels.")]
        public int manualAtlasWidthPixels = 1024;

        [Tooltip("Used when atlasLayoutMode = ManualPixels.")]
        public int manualAtlasHeightPixels = 1024;

        [Tooltip("Used when atlasLayoutMode = ManualGrid. Grid columns for auto-populate and UV layout.")]
        public int manualColumns = 8;

        [Tooltip("Used when atlasLayoutMode = ManualGrid if inferRowsFromTileCount is false.")]
        public int manualRows = 8;

        [Tooltip("Used when atlasLayoutMode = ManualGrid. If true, rows are computed from tile count.")]
        public bool inferRowsFromTileCount = true;

        [Tooltip("Used when atlasLayoutMode = ManualGrid. Total tiles to auto-populate (entries will be created/trimmed to this).")]
        public int manualTileCount = 64;

        #endregion

        #region Auto-Populate / Ids

        [Header("Auto-Populate")]
        [Tooltip("If true, auto-populate reserves tileId=0 for 'empty/default' and starts real tiles at 1.")]
        public bool reserveZeroForEmpty = true;

        [Tooltip("If reserveZeroForEmpty is false, auto-populate will start from this id. If true, it starts at 1.")]
        public int autoIdStart = 0;

        #endregion

        #region Tile Definitions

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
            [Tooltip("Semantic base color for this tile.")]
            public Color32 color = new Color32(255, 255, 255, 255);

            [Tooltip("Optional per-cell brightness jitter amplitude for this tile (0..0.25 recommended).")]
            [Range(0f, 0.25f)]
            public float colorJitter = 0.05f;

            [Tooltip("How much to blend tint into the sprite. 0 = no tint, 1 = full tint.")]
            [Range(0f, 1f)]
            public float colorBlend = 1f;

            [Header("Tags (Optional)")]
            [Tooltip("Optional tags for debugging/querying.")]
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

        #endregion

        #region Effective Atlas Sizing Helpers

        public bool TryGetEffectiveAtlasSize(out int widthPx, out int heightPx)
        {
            switch (atlasLayoutMode)
            {
                case AtlasLayoutMode.FromTexture:
                    return tryGetSizeFromTexture(out widthPx, out heightPx);

                case AtlasLayoutMode.ManualPixels:
                    widthPx = Mathf.Max(1, manualAtlasWidthPixels);
                    heightPx = Mathf.Max(1, manualAtlasHeightPixels);
                    return true;

                case AtlasLayoutMode.ManualGrid:
                    return tryGetSizeFromManualGrid(out widthPx, out heightPx);

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

            return tilesX > 0 && tilesY > 0;
        }

        private bool tryGetSizeFromTexture(out int widthPx, out int heightPx)
        {
            if (atlasTexture == null)
            {
                widthPx = 0;
                heightPx = 0;
                return false;
            }

            widthPx = atlasTexture.width;
            heightPx = atlasTexture.height;
            return widthPx > 0 && heightPx > 0;
        }

        private bool tryGetSizeFromManualGrid(out int widthPx, out int heightPx)
        {
            int cols = Mathf.Max(1, manualColumns);
            int rows = getManualGridRows(cols);

            int stepX = Mathf.Max(1, tilePixelSize.x + Mathf.Max(0, paddingPixels.x));
            int stepY = Mathf.Max(1, tilePixelSize.y + Mathf.Max(0, paddingPixels.y));

            widthPx = cols * stepX;
            heightPx = rows * stepY;
            return true;
        }

        private int getManualGridRows(int cols)
        {
            if (inferRowsFromTileCount)
                return Mathf.CeilToInt(Mathf.Max(1, manualTileCount) / (float)cols);

            return Mathf.Max(1, manualRows);
        }

        #endregion

        #region Validation

        public void ValidateOrThrow()
        {
            validateLayoutSettingsOrThrow();
            validateEntriesOrThrow();
        }

        private void validateLayoutSettingsOrThrow()
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
        }

        private void validateEntriesOrThrow()
        {
            if (entries == null)
                return;

            var ids = new HashSet<int>();

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null)
                    continue;

                if (!ids.Add(e.tileId))
                    throw new InvalidOperationException($"{name}: Duplicate tileId found: {e.tileId}.");

                if (e.tileSpan.x <= 0 || e.tileSpan.y <= 0)
                    throw new ArgumentOutOfRangeException(nameof(Entry.tileSpan), $"{name}: tileSpan must be >= (1,1).");
            }
        }

        #endregion

        #region Runtime Build

        public TileLibrary BuildRuntime()
        {
            ValidateOrThrow();

            if (!TryGetEffectiveAtlasSize(out int atlasW, out int atlasH))
            {
                throw new InvalidOperationException(
                    $"{name}: Cannot BuildRuntime() because atlasLayoutMode={atlasLayoutMode} has no effective atlas size. " +
                    $"Assign an atlasTexture or use ManualPixels/ManualGrid.");
            }

            var defs = new Dictionary<int, TileDef>(entries != null ? entries.Count : 0);

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e == null)
                        continue;

                    var def = buildTileDef(e, atlasW, atlasH);
                    defs[e.tileId] = def;
                }
            }

            // v1: defaultTileId is conventionally 0. WorldHost and auto-populate also assume 0 is empty.
            return new TileLibrary(defs, defaultTileId: 0);
        }

        private TileDef buildTileDef(Entry e, int atlasW, int atlasH)
        {
            string entryName = string.IsNullOrWhiteSpace(e.name) ? $"Tile {e.tileId}" : e.name;
            RectUv uv = resolveUv(e, atlasW, atlasH);
            List<TileProperty> runtimeProps = buildRuntimeProperties(e);

            return new TileDef(
                e.tileId,
                entryName,
                uv,
                e.tags,
                runtimeProps
            );
        }

        private RectUv resolveUv(Entry e, int atlasW, int atlasH)
        {
            if (e.overrideUv)
                return new RectUv(e.uvMin.x, e.uvMin.y, e.uvMax.x, e.uvMax.y);

            return ComputeUvFromTileCoord(
                atlasW,
                atlasH,
                tilePixelSize,
                paddingPixels,
                originTopLeft,
                e.tileCoord,
                e.tileSpan
            );
        }

        private List<TileProperty> buildRuntimeProperties(Entry e)
        {
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

            // Inject tile color semantics without mutating asset data.
            runtimeProps.Add(new TileColorProperty(e.color, e.colorJitter, e.colorBlend));
            return runtimeProps;
        }

        #endregion

        #region UV Computation

        public static RectUv ComputeUvFromTileCoord(
            int atlasWidthPx,
            int atlasHeightPx,
            Vector2Int tileSizePx,
            Vector2Int paddingPx,
            bool originTopLeft,
            Vector2Int tileCoord,
            Vector2Int tileSpan)
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

        #endregion

        #region Editor Convenience

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
            if (!validateAutoPopulateInputs())
                return;

            if (!tryResolveAutoPopulateGrid(out int tilesX, out int tilesY))
                return;

            int desiredCount = computeDesiredAutoPopulateCount(tilesX, tilesY);

            entries.Clear();
            populateEntries(tilesX, tilesY, desiredCount);

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif

            UnityEngine.Debug.Log($"{name}: Auto-populated {desiredCount} entries (grid {tilesX}x{tilesY}, mode={atlasLayoutMode}).", this);
        }

        private bool validateAutoPopulateInputs()
        {
            if (tilePixelSize.x <= 0 || tilePixelSize.y <= 0)
            {
                UnityEngine.Debug.LogError($"{name}: tilePixelSize must be > 0.", this);
                return false;
            }

            if (paddingPixels.x < 0 || paddingPixels.y < 0)
            {
                UnityEngine.Debug.LogError($"{name}: paddingPixels cannot be negative.", this);
                return false;
            }

            return true;
        }

        private bool tryResolveAutoPopulateGrid(out int tilesX, out int tilesY)
        {
            tilesX = 0;
            tilesY = 0;

            if (atlasLayoutMode == AtlasLayoutMode.ManualGrid)
            {
                tilesX = Mathf.Max(1, manualColumns);
                tilesY = Mathf.Max(1, inferRowsFromTileCount
                    ? Mathf.CeilToInt(Mathf.Max(1, manualTileCount) / (float)tilesX)
                    : Mathf.Max(1, manualRows));

                return true;
            }

            if (!TryGetEffectiveGridSize(out tilesX, out tilesY))
            {
                UnityEngine.Debug.LogError(
                    $"{name}: Cannot auto-populate because no effective atlas/grid size is available. " +
                    $"Assign atlasTexture (FromTexture) or use ManualPixels/ManualGrid.",
                    this);
                return false;
            }

            return true;
        }

        private int computeDesiredAutoPopulateCount(int tilesX, int tilesY)
        {
            int capacity = tilesX * tilesY;

            if (atlasLayoutMode == AtlasLayoutMode.ManualGrid)
                return Mathf.Clamp(manualTileCount, 1, capacity);

            return capacity;
        }

        private void populateEntries(int tilesX, int tilesY, int desiredCount)
        {
            int nextId = reserveZeroForEmpty ? 1 : autoIdStart;

            for (int i = 0; i < desiredCount; i++)
            {
                int x = i % tilesX;
                int y = i / tilesX;

                entries.Add(createAutoEntry(nextId++, i, x, y));
            }
        }

        private Entry createAutoEntry(int tileId, int index, int x, int y)
        {
            return new Entry
            {
                tileId = tileId,
                name = $"Tile {index}",
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
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            clampAuthoringValues();

            if (autoCreateAtlasMaterial && atlasTexture != null)
            {
                // Intentionally not auto-invoked by default in v1 polish.
                // Use a context menu or manual call if you want to generate the material asset.
                // EnsureAtlasMaterialExistsOrUpdated();
            }
        }

        private void clampAuthoringValues()
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
        }

        private void EnsureAtlasMaterialExistsOrUpdated()
        {
            if (atlasTexture == null)
                return;

            Shader shader = resolveAutoMaterialShader();
            if (shader == null)
            {
                UnityEngine.Debug.LogWarning($"{name}: Could not find a usable shader for atlas material creation.", this);
                return;
            }

            if (atlasMaterial != null)
            {
                updateExistingAtlasMaterial(shader);
                return;
            }

            createAtlasMaterialAsset(shader);
        }

        private Shader resolveAutoMaterialShader()
        {
            Shader shader = Shader.Find(autoMaterialShaderName);
            if (shader != null)
                return shader;

            return Shader.Find("WorldGrid/Unlit Vertex Tint Blend")
                   ?? Shader.Find("Sprites/Default")
                   ?? Shader.Find("Unlit/Transparent")
                   ?? Shader.Find("Unlit/Texture");
        }

        private void updateExistingAtlasMaterial(Shader shader)
        {
            if (atlasMaterial.shader != shader)
                atlasMaterial.shader = shader;

            bindAtlasTexture(atlasMaterial, atlasTexture);
            EditorUtility.SetDirty(atlasMaterial);
        }

        private void createAtlasMaterialAsset(Shader shader)
        {
            string assetPath = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(assetPath))
                return;

            string folder = System.IO.Path.GetDirectoryName(assetPath);
            string atlasName = atlasTexture != null ? atlasTexture.name : "Atlas";
            string assetName = name;

            string matName = $"{atlasName}__{assetName}{autoMaterialSuffix}.mat";
            string matPath = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.Combine(folder, matName));

            var mat = new Material(shader);
            bindAtlasTexture(mat, atlasTexture);

            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();

            atlasMaterial = mat;
            EditorUtility.SetDirty(this);

            UnityEngine.Debug.Log($"{name}: Created atlasMaterial at '{matPath}' using shader '{shader.name}'.", this);
        }

        private void bindAtlasTexture(Material mat, Texture2D tex)
        {
            if (mat == null || tex == null)
                return;

            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", tex);

            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", tex);
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

            int updated = computeAndStoreUvs(atlasW, atlasH);

            EditorUtility.SetDirty(this);
            UnityEngine.Debug.Log($"{name}: Computed UVs for {updated} entries (stored into uvMin/uvMax).", this);
        }

        private int computeAndStoreUvs(int atlasW, int atlasH)
        {
            int updated = 0;

            if (entries == null)
                return 0;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null)
                    continue;

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

            return updated;
        }
#endif

        #endregion
    }
}
