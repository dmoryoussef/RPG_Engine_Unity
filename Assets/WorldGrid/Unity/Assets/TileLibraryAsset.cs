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
        [Header("Atlas")]
        [Tooltip("The atlas/spritesheet texture (PNG).")]
        public Texture2D atlasTexture;

        [Tooltip("Optional: material that uses the atlasTexture. If null, it can be auto-created in editor.")]
        public Material atlasMaterial;

        [Header("Atlas Material Auto-Create (Editor)")]
        [Tooltip("If true, the asset will auto-create/update atlasMaterial in the Editor when possible.")]
        public bool autoCreateAtlasMaterial = true;

        [Tooltip("Material asset name suffix (created next to this TileLibraryAsset).")]
        public string autoMaterialSuffix = "_WorldGrid_Mat";

        [Tooltip("Preferred shader name for the auto-created material. " +
                 "Examples: 'Universal Render Pipeline/Unlit', 'Sprites/Default', 'Unlit/Transparent'.")]
        public string autoMaterialShaderName = "Universal Render Pipeline/Unlit";

        [Header("Atlas Layout")]
        [Tooltip("Tile size in pixels (e.g., 16x16, 32x32).")]
        public Vector2Int tilePixelSize = new Vector2Int(32, 32);

        [Tooltip("Optional padding/gutter between tiles in pixels (0 for tightly packed atlases).")]
        public Vector2Int paddingPixels = Vector2Int.zero;

        [Tooltip("If true, tile coords are authored with (0,0) at the TOP-LEFT of the atlas grid.\n" +
                 "If false, (0,0) is BOTTOM-LEFT (matches UV space).")]
        public bool originTopLeft = true;

        [Header("Auto-Populate")]
        [Tooltip("If true, auto-populate will reserve tileId=0 for 'empty/default' and start real tiles at 1.")]
        public bool reserveZeroForEmpty = true;

        [Tooltip("If reserveZeroForEmpty is false, auto-populate will start from this id. If true, it starts at 1.")]
        public int autoIdStart = 0;

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

            [Tooltip("Optional tags for debugging/querying (string-based for now).")]
            public List<string> tags = new();

            [Header("Properties (Optional)")]
            [Tooltip("Optional modular semantics for this tile type (e.g., MovementProperty).")]
            [SerializeReference]
            public List<TileProperty> properties = new();

            [Header("Advanced")]
            [Tooltip("If enabled, uvMin/uvMax are used directly instead of computing from tileCoord/tileSpan.")]
            public bool overrideUv = false;

            [Tooltip("Normalized UV min (0..1), bottom-left.")]
            public Vector2 uvMin;

            [Tooltip("Normalized UV max (0..1), top-right.")]
            public Vector2 uvMax;
        }

        /// <summary>
        /// Creates a pure runtime TileLibrary from this asset.
        /// </summary>
        public TileLibrary BuildRuntime()
        {
            ValidateOrThrow();

#if UNITY_EDITOR
            if (autoCreateAtlasMaterial)
            {
                EnsureAtlasMaterialExistsOrUpdated();
            }
#endif

            var lib = new TileLibrary();
            int atlasW = atlasTexture.width;
            int atlasH = atlasTexture.height;

            foreach (var e in entries)
            {
                if (e == null) continue;

                // Name is optional for auto-populate workflows, but TileDef requires a non-null string.
                string entryName = string.IsNullOrWhiteSpace(e.name) ? $"tile_{e.tileId}" : e.name;

                RectUv uv;
                if (e.overrideUv)
                {
                    uv = new RectUv(e.uvMin.x, e.uvMin.y, e.uvMax.x, e.uvMax.y);
                }
                else
                {
                    uv = ComputeUvFromTileCoord(
                        atlasW, atlasH,
                        tilePixelSize, paddingPixels,
                        originTopLeft,
                        e.tileCoord, e.tileSpan
                    );
                }

                // IMPORTANT: properties now flow from Entry -> runtime TileDef
                lib.Set(new TileDef(
                    e.tileId,
                    entryName,
                    uv,
                    e.tags,
                    e.properties
                ));
            }

            lib.FinalizeBuild();
            return lib;
        }

        /// <summary>
        /// Computes normalized UVs from an atlas grid coordinate and span.
        /// </summary>
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
            if (tileSizePx.x <= 0 || tileSizePx.y <= 0) throw new ArgumentOutOfRangeException(nameof(tileSizePx), "Tile size must be > 0");
            if (paddingPx.x < 0 || paddingPx.y < 0) throw new ArgumentOutOfRangeException(nameof(paddingPx), "Padding cannot be negative");
            if (tileSpan.x <= 0 || tileSpan.y <= 0) throw new ArgumentOutOfRangeException(nameof(tileSpan), "Tile span must be >= 1");

            int stepX = tileSizePx.x + paddingPx.x;
            int stepY = tileSizePx.y + paddingPx.y;

            int tilesX = atlasWidthPx / stepX;
            int tilesY = atlasHeightPx / stepY;

            int tx = tileCoord.x;
            int ty = tileCoord.y;

            if (originTopLeft)
                ty = (tilesY - 1) - ty;

            int pxMin = tx * stepX;
            int pyMin = ty * stepY;

            int pxMax = pxMin + (tileSpan.x * tileSizePx.x);
            int pyMax = pyMin + (tileSpan.y * tileSizePx.y);

            float uMin = (float)pxMin / atlasWidthPx;
            float vMin = (float)pyMin / atlasHeightPx;
            float uMax = (float)pxMax / atlasWidthPx;
            float vMax = (float)pyMax / atlasHeightPx;

            return new RectUv(uMin, vMin, uMax, vMax);
        }

        public void ValidateOrThrow()
        {
            if (atlasTexture == null)
                throw new InvalidOperationException($"{name}: atlasTexture is not assigned.");

            if (tilePixelSize.x <= 0 || tilePixelSize.y <= 0)
                throw new InvalidOperationException($"{name}: tilePixelSize must be > 0.");

            if (paddingPixels.x < 0 || paddingPixels.y < 0)
                throw new InvalidOperationException($"{name}: paddingPixels cannot be negative.");

            int stepX = tilePixelSize.x + paddingPixels.x;
            int stepY = tilePixelSize.y + paddingPixels.y;

            if (stepX <= 0 || stepY <= 0)
                throw new InvalidOperationException($"{name}: invalid atlas step size.");
        }

        private int GetAutoPopulateStartId()
        {
            if (reserveZeroForEmpty)
                return 1;
            return autoIdStart;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!autoCreateAtlasMaterial) return;

            // Avoid spamming creation when asset is incomplete.
            if (atlasTexture == null) return;

            EnsureAtlasMaterialExistsOrUpdated();
        }

        private void EnsureAtlasMaterialExistsOrUpdated()
        {
            // If atlasMaterial exists, ensure it points at the atlas texture.
            if (atlasMaterial != null)
            {
                if (atlasMaterial.mainTexture != atlasTexture)
                {
                    atlasMaterial.mainTexture = atlasTexture;
                    EditorUtility.SetDirty(atlasMaterial);
                }
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

            Shader shader = Shader.Find(autoMaterialShaderName);
            if (shader == null)
            {
                // Fallbacks for different pipelines.
                shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Unlit/Texture");
            }

            if (shader == null)
            {
                UnityEngine.Debug.LogWarning($"{name}: Could not find a shader to create atlas material. Please assign atlasMaterial manually.", this);
                return;
            }

            var mat = new Material(shader)
            {
                name = System.IO.Path.GetFileNameWithoutExtension(matPath)
            };
            mat.mainTexture = atlasTexture;

            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();

            atlasMaterial = mat;
            EditorUtility.SetDirty(this);

            UnityEngine.Debug.Log($"{name}: Auto-created atlas material at {matPath}", this);
        }

        [ContextMenu("Validate (Log)")]
        private void Validate_Log()
        {
            try
            {
                ValidateOrThrow();

                int stepX = tilePixelSize.x + paddingPixels.x;
                int stepY = tilePixelSize.y + paddingPixels.y;

                int tilesX = atlasTexture.width / stepX;
                int tilesY = atlasTexture.height / stepY;

                UnityEngine.Debug.Log($"{name}: TileLibraryAsset validation OK. Atlas {atlasTexture.width}x{atlasTexture.height}px, tile {tilePixelSize.x}x{tilePixelSize.y}px, grid {tilesX}x{tilesY}.", this);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"{name}: TileLibraryAsset validation FAILED: {ex.Message}", this);
            }
        }

        [ContextMenu("Auto-Populate Entries From Atlas Layout")]
        private void AutoPopulateEntriesFromAtlasLayout()
        {
            if (atlasTexture == null)
            {
                UnityEngine.Debug.LogError($"{name}: atlasTexture is not assigned.", this);
                return;
            }

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

            int atlasW = atlasTexture.width;
            int atlasH = atlasTexture.height;

            int stepX = tilePixelSize.x + paddingPixels.x;
            int stepY = tilePixelSize.y + paddingPixels.y;

            int tilesX = atlasW / stepX;
            int tilesY = atlasH / stepY;

            if (tilesX <= 0 || tilesY <= 0)
            {
                UnityEngine.Debug.LogError($"{name}: Computed tilesX/tilesY invalid. Check tilePixelSize/paddingPixels.", this);
                return;
            }

            entries.Clear();

            int id = GetAutoPopulateStartId();

            for (int y = 0; y < tilesY; y++)
                for (int x = 0; x < tilesX; x++)
                {
                    var e = new Entry
                    {
                        tileId = id,
                        name = string.Empty,
                        tileCoord = new Vector2Int(x, y),
                        tileSpan = Vector2Int.one,
                        tags = new List<string>(),
                        properties = new List<TileProperty>(),
                        overrideUv = false,
                        uvMin = Vector2.zero,
                        uvMax = Vector2.zero
                    };

                    entries.Add(e);
                    id++;
                }

            EditorUtility.SetDirty(this);
            UnityEngine.Debug.Log($"{name}: Auto-populated {entries.Count} entries ({tilesX}x{tilesY}). " +
                      $"tileId start={GetAutoPopulateStartId()} (reserveZeroForEmpty={reserveZeroForEmpty}).", this);
        }

        [ContextMenu("Compute UVs Into uvMin/uvMax (Visibility Helper)")]
        private void ComputeUvsIntoInspectorFields()
        {
            if (atlasTexture == null)
            {
                UnityEngine.Debug.LogError($"{name}: atlasTexture is not assigned.", this);
                return;
            }

            int atlasW = atlasTexture.width;
            int atlasH = atlasTexture.height;

            int updated = 0;

            foreach (var e in entries)
            {
                if (e == null) continue;
                if (e.overrideUv) continue;

                RectUv uv;
                try
                {
                    uv = ComputeUvFromTileCoord(
                        atlasW, atlasH,
                        tilePixelSize, paddingPixels,
                        originTopLeft,
                        e.tileCoord, e.tileSpan
                    );
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"{name}: Could not compute UV for tileId={e.tileId} ({e.name}): {ex.Message}", this);
                    continue;
                }

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
