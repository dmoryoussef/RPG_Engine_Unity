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
        #region Atlas (Authoring)

        [Header("Atlas")]
        [Tooltip("The atlas/spritesheet texture (PNG). Used for editor-time BaseColor averaging and UV validation.")]
        public Texture2D atlasTexture;

        [Header("Rendering")]
        [Tooltip("Optional template material. The provider will Instantiate() this at runtime. " +
                 "If null, the provider will fall back to its default template/shader.")]
        public Material templateMaterial;

        [Tooltip("Optional height atlas (grayscale). Must match atlasTexture layout/UVs. " +
                 "If null, tiles render flat.")]
        public Texture2D heightAtlasTexture;


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
            "If true, tile coords are authored with (0,0) at the TOP-LEFT of the atlas.\n" +
            "If false, tile coords are authored with (0,0) at the BOTTOM-LEFT.")]
        public bool originTopLeft = true;

        [Header("Manual Atlas Size (Pixels)")]
        [Tooltip("Only used when AtlasLayoutMode = ManualPixels.")]
        public Vector2Int manualAtlasPixelSize = new Vector2Int(1024, 1024);

        [Header("Manual Grid (Tiles)")]
        [Tooltip("Only used when AtlasLayoutMode = ManualGrid. Auto-populate uses these dimensions.")]
        public Vector2Int manualGridSize = new Vector2Int(16, 16);

        [Tooltip("Only used when AtlasLayoutMode = ManualGrid. If > 0, auto-populate will create this many entries total.")]
        public int manualTileCount = 0;

        [Tooltip("Auto-populate: first tileId assigned.")]
        public int autoIdStart = 0;

        [Tooltip("Auto-populate: optional name prefix.")]
        public string autoNamePrefix = "Tile ";

        #endregion

        #region Entries

        public enum BaseColorMode : byte
        {
            Solid = 0,
            AverageAtlas = 1
        }

        [Serializable]
        public sealed class Entry
        {
            public int tileId;
            public string name;

            [Header("Atlas Addressing")]
            public Vector2Int tileCoord = Vector2Int.zero;
            public Vector2Int tileSpan = Vector2Int.one;

            [Header("UV Override")]
            public bool overrideUv = false;
            public Vector2 uvMin;
            public Vector2 uvMax;

            [Header("Base Color (Intrinsic / Low Detail)")]
            public BaseColorMode baseColorMode = BaseColorMode.AverageAtlas;
            public Color32 baseColorSolid = new Color32(255, 255, 255, 255);
            public Color32 baseColorCachedAverage = new Color32(255, 255, 255, 255);
            [Range(0f, 1f)] public float baseColorInfluence = 1f;

            [Header("Tint (Overlay / Art Direction)")]
            public Color32 color = new Color32(255, 255, 255, 255);
            [Range(0f, 0.25f)] public float colorJitter = 0.05f;
            [Range(0f, 1f)] public float colorBlend = 1f;

            [Header("Tags (Optional)")]
            public List<string> tags = new();

            [Header("Properties (Optional)")]
            [SerializeReference] public List<TileProperty> properties = new();
        }

        [Header("Tiles")]
        public List<Entry> entries = new();

        #endregion

        #region Effective Atlas Size

        public bool TryGetEffectiveAtlasSize(out int widthPx, out int heightPx)
        {
            widthPx = 0;
            heightPx = 0;

            switch (atlasLayoutMode)
            {
                case AtlasLayoutMode.FromTexture:
                    if (atlasTexture == null)
                        return false;

                    widthPx = atlasTexture.width;
                    heightPx = atlasTexture.height;
                    return widthPx > 0 && heightPx > 0;

                case AtlasLayoutMode.ManualPixels:
                    widthPx = Mathf.Max(1, manualAtlasPixelSize.x);
                    heightPx = Mathf.Max(1, manualAtlasPixelSize.y);
                    return true;

                case AtlasLayoutMode.ManualGrid:
                    int cols = Mathf.Max(1, manualGridSize.x);
                    int rows = Mathf.Max(1, manualGridSize.y);

                    int tileW = Mathf.Max(1, tilePixelSize.x);
                    int tileH = Mathf.Max(1, tilePixelSize.y);

                    int padX = Mathf.Max(0, paddingPixels.x);
                    int padY = Mathf.Max(0, paddingPixels.y);

                    widthPx = (tileW * cols) + (padX * Mathf.Max(0, cols - 1));
                    heightPx = (tileH * rows) + (padY * Mathf.Max(0, rows - 1));
                    return true;

                default:
                    return false;
            }
        }

        #endregion

        #region Validation

        public void ValidateOrThrow()
        {
            if (tilePixelSize.x <= 0 || tilePixelSize.y <= 0)
                throw new InvalidOperationException($"{name}: tilePixelSize must be > 0.");

            if (paddingPixels.x < 0 || paddingPixels.y < 0)
                throw new InvalidOperationException($"{name}: paddingPixels cannot be negative.");

            if (atlasLayoutMode == AtlasLayoutMode.ManualPixels)
            {
                if (manualAtlasPixelSize.x <= 0 || manualAtlasPixelSize.y <= 0)
                    throw new InvalidOperationException($"{name}: manualAtlasPixelSize must be > 0 for ManualPixels.");
            }

            if (atlasLayoutMode == AtlasLayoutMode.ManualGrid)
            {
                if (manualGridSize.x <= 0 || manualGridSize.y <= 0)
                    throw new InvalidOperationException($"{name}: manualGridSize must be > 0 for ManualGrid.");
            }

            // Height atlas (optional) must match the main atlas dimensions if both are provided.
            if (atlasTexture != null && heightAtlasTexture != null)
            {
                if (atlasTexture.width != heightAtlasTexture.width || atlasTexture.height != heightAtlasTexture.height)
                {
                    throw new InvalidOperationException(
                        $"{name}: heightAtlasTexture must match atlasTexture dimensions. " +
                        $"atlas={atlasTexture.width}x{atlasTexture.height}, height={heightAtlasTexture.width}x{heightAtlasTexture.height}");
                }
            }
        }

        private void clampAuthoringValues()
        {
            tilePixelSize.x = Mathf.Max(1, tilePixelSize.x);
            tilePixelSize.y = Mathf.Max(1, tilePixelSize.y);

            paddingPixels.x = Mathf.Max(0, paddingPixels.x);
            paddingPixels.y = Mathf.Max(0, paddingPixels.y);

            manualAtlasPixelSize.x = Mathf.Max(1, manualAtlasPixelSize.x);
            manualAtlasPixelSize.y = Mathf.Max(1, manualAtlasPixelSize.y);

            manualGridSize.x = Mathf.Max(1, manualGridSize.x);
            manualGridSize.y = Mathf.Max(1, manualGridSize.y);

            if (manualTileCount < 0) manualTileCount = 0;
            if (autoIdStart < 0) autoIdStart = 0;
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
                    "Assign atlasTexture or use ManualPixels/ManualGrid.");
            }

            var defs = new Dictionary<int, TileDef>(entries != null ? entries.Count : 0);

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e == null)
                        continue;

                    string entryName = string.IsNullOrWhiteSpace(e.name) ? $"Tile {e.tileId}" : e.name;
                    RectUv uv = resolveUv(e, atlasW, atlasH);

                    Color32 baseColor = resolveBaseColor(e);
                    float baseInfluence = Mathf.Clamp01(e.baseColorInfluence);

                    List<TileProperty> runtimeProps = buildRuntimeProperties(e);

                    var def = new TileDef(
                        e.tileId,
                        entryName,
                        uv,
                        baseColor,
                        baseInfluence,
                        e.tags,
                        runtimeProps
                    );

                    defs[e.tileId] = def;
                }
            }

            return new TileLibrary(defs, defaultTileId: 0);
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

        private static Color32 resolveBaseColor(Entry e)
        {
            switch (e.baseColorMode)
            {
                case BaseColorMode.Solid:
                    return e.baseColorSolid;

                case BaseColorMode.AverageAtlas:
                    return e.baseColorCachedAverage;

                default:
                    return new Color32(255, 255, 255, 255);
            }
        }

        private static List<TileProperty> buildRuntimeProperties(Entry e)
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

            // Inject tint semantics as a property.
            runtimeProps.Add(new TileColorProperty(e.color, e.colorJitter, e.colorBlend));
            return runtimeProps;
        }

        #endregion

        #region UV Math

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
                throw new ArgumentOutOfRangeException(nameof(tileSpan), "Tile span must be > 0.");

            int tileW = tileSizePx.x;
            int tileH = tileSizePx.y;

            int padX = paddingPx.x;
            int padY = paddingPx.y;

            int xPxMin = tileCoord.x * (tileW + padX);
            int yPxMin = tileCoord.y * (tileH + padY);

            int spanW = (tileSpan.x * tileW) + (Mathf.Max(0, tileSpan.x - 1) * padX);
            int spanH = (tileSpan.y * tileH) + (Mathf.Max(0, tileSpan.y - 1) * padY);

            int xPxMax = xPxMin + spanW;
            int yPxMax = yPxMin + spanH;

            float uMin = (float)xPxMin / atlasWidthPx;
            float uMax = (float)xPxMax / atlasWidthPx;

            float vMin;
            float vMax;

            if (originTopLeft)
            {
                float yTop = (float)yPxMin / atlasHeightPx;
                float yBottom = (float)yPxMax / atlasHeightPx;

                vMax = 1f - yTop;
                vMin = 1f - yBottom;
            }
            else
            {
                vMin = (float)yPxMin / atlasHeightPx;
                vMax = (float)yPxMax / atlasHeightPx;
            }

            uMin = Mathf.Clamp01(uMin);
            uMax = Mathf.Clamp01(uMax);
            vMin = Mathf.Clamp01(vMin);
            vMax = Mathf.Clamp01(vMax);

            return new RectUv(uMin, vMin, uMax, vMax);
        }

        #endregion

#if UNITY_EDITOR
        #region Editor Actions

        [ContextMenu("Auto-Populate Entries From Atlas Layout")]
        public void AutoPopulateEntriesFromAtlasLayout()
        {
            clampAuthoringValues();
            ValidateOrThrow();

            if (!TryGetEffectiveAtlasSize(out int atlasW, out int atlasH))
            {
                UnityEngine.Debug.LogError($"{name}: Cannot auto-populate because there is no effective atlas size.", this);
                return;
            }

            int cols;
            int rows;

            if (atlasLayoutMode == AtlasLayoutMode.ManualGrid)
            {
                cols = Mathf.Max(1, manualGridSize.x);
                rows = Mathf.Max(1, manualGridSize.y);
            }
            else
            {
                // derive approximate grid from atlas pixel size (best-effort)
                int stepX = tilePixelSize.x + paddingPixels.x;
                int stepY = tilePixelSize.y + paddingPixels.y;

                cols = Mathf.Max(1, stepX > 0 ? (atlasW + paddingPixels.x) / stepX : 1);
                rows = Mathf.Max(1, stepY > 0 ? (atlasH + paddingPixels.y) / stepY : 1);
            }

            int total = (manualTileCount > 0) ? manualTileCount : (cols * rows);
            total = Mathf.Max(1, total);

            if (entries == null)
                entries = new List<Entry>(total);
            else
                entries.Clear();

            for (int i = 0; i < total; i++)
            {
                int id = autoIdStart + i;

                int x = (cols > 0) ? (i % cols) : 0;
                int y = (cols > 0) ? (i / cols) : 0;

                entries.Add(new Entry
                {
                    tileId = id,
                    name = $"{autoNamePrefix}{id}",
                    tileCoord = new Vector2Int(x, y),
                    tileSpan = Vector2Int.one,
                    overrideUv = false,

                    baseColorMode = BaseColorMode.AverageAtlas,
                    baseColorSolid = new Color32(255, 255, 255, 255),
                    baseColorCachedAverage = new Color32(255, 255, 255, 255),
                    baseColorInfluence = 1f,

                    color = new Color32(255, 255, 255, 255),
                    colorJitter = 0.05f,
                    colorBlend = 1f
                });
            }

            EditorUtility.SetDirty(this);
            UnityEngine.Debug.Log($"{name}: Auto-populated {entries.Count} entries.", this);
        }

        [ContextMenu("Compute UVs For Entries (Store Into uvMin/uvMax)")]
        public void ComputeAndStoreUvsForEntries()
        {
            clampAuthoringValues();
            ValidateOrThrow();

            if (!TryGetEffectiveAtlasSize(out int atlasW, out int atlasH))
            {
                UnityEngine.Debug.LogError($"{name}: Cannot compute UVs because there is no effective atlas size.", this);
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
                Entry e = entries[i];
                if (e == null)
                    continue;

                RectUv uv = resolveUv(e, atlasW, atlasH);

                e.overrideUv = true;
                e.uvMin = new Vector2(uv.UMin, uv.VMin);
                e.uvMax = new Vector2(uv.UMax, uv.VMax);
                updated++;
            }

            return updated;
        }

        [ContextMenu("Recompute BaseColor Cached Averages From Atlas")]
        public void RecomputeBaseColorCachedAveragesFromAtlas()
        {
            clampAuthoringValues();
            ValidateOrThrow();

            if (atlasTexture == null)
            {
                UnityEngine.Debug.LogWarning($"{name}: atlasTexture is null.", this);
                return;
            }

            if (!atlasTexture.isReadable)
            {
                UnityEngine.Debug.LogWarning($"{name}: atlasTexture is not readable. Enable Read/Write in import settings.", this);
                return;
            }

            if (!TryGetEffectiveAtlasSize(out int atlasW, out int atlasH))
            {
                UnityEngine.Debug.LogWarning($"{name}: No effective atlas size; cannot compute base colors.", this);
                return;
            }

            int texW = atlasTexture.width;
            int texH = atlasTexture.height;

            Color32[] pixels = atlasTexture.GetPixels32();

            int updated = 0;
            int failed = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (e == null)
                    continue;

                RectUv uv = resolveUv(e, atlasW, atlasH);

                if (!TryComputeAverageFromUvRect(pixels, texW, texH, uv, out var avg, alphaThreshold: 1))
                {
                    failed++;
                    continue;
                }

                e.baseColorCachedAverage = avg;
                updated++;
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();

            UnityEngine.Debug.Log($"{name}: BaseColor averages recomputed. updated={updated}, failed={failed}", this);
        }

        private static bool TryComputeAverageFromUvRect(
            Color32[] pixels,
            int texW,
            int texH,
            RectUv uv,
            out Color32 avg,
            byte alphaThreshold)
        {
            avg = new Color32(255, 255, 255, 255);

            int xMin = Mathf.Clamp(Mathf.FloorToInt(uv.UMin * texW), 0, texW - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(uv.UMax * texW), 0, texW);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(uv.VMin * texH), 0, texH - 1);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(uv.VMax * texH), 0, texH);

            if (xMax <= xMin || yMax <= yMin)
                return false;

            long sumR = 0;
            long sumG = 0;
            long sumB = 0;
            long sumA = 0;

            for (int y = yMin; y < yMax; y++)
            {
                int row = y * texW;

                for (int x = xMin; x < xMax; x++)
                {
                    Color32 c = pixels[row + x];
                    if (c.a < alphaThreshold)
                        continue;

                    sumR += c.r * c.a;
                    sumG += c.g * c.a;
                    sumB += c.b * c.a;
                    sumA += c.a;
                }
            }

            if (sumA <= 0)
                return false;

            avg = new Color32(
                (byte)(sumR / sumA),
                (byte)(sumG / sumA),
                (byte)(sumB / sumA),
                255);

            return true;
        }

        #endregion
#endif
    }
}
