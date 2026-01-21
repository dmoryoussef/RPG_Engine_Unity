using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGrid.Unity.Assets;

namespace WorldGrid.Runtime.Tiles
{
    /// <summary>
    /// World-level authority for resolving tile libraries by key.
    /// Phase 1: forwards atlas + runtime library building to TileLibraryAsset,
    /// but centralizes access, caching, and material instancing.
    /// </summary>
    public sealed class TileLibraryProvider : MonoBehaviour, ITileLibrarySource
    {
        [SerializeField] private List<TileLibraryProviderEntry> entries = new();

        // Key string -> view (stable per world per key)
        private readonly Dictionary<string, ITileLibraryView> _views = new(StringComparer.Ordinal);

        // Track instanced materials so we can clean them up.
        private readonly List<Material> _ownedMaterials = new();

        // NEW: Track runtime textures so we can clean them up (procedural/manual color atlases).
        private readonly List<Texture2D> _ownedTextures = new();

        public bool Has(TileLibraryKey key) =>
            !key.IsEmpty && TryGetEntry(key, out _);

        public ITileLibraryView Get(TileLibraryKey key)
        {
            if (key.IsEmpty)
                throw new ArgumentException("TileLibraryKey is empty.", nameof(key));

            if (_views.TryGetValue(key.Value, out var cached))
                return cached;

            if (!TryGetEntry(key, out var entry) || entry.asset == null)
                throw new KeyNotFoundException($"TileLibraryProvider: missing key '{key.Value}'.");

            var asset = entry.asset;

            // Runtime library is built from the asset (semantics + UVs).
            TileLibrary runtimeLib = asset.BuildRuntime();

            // Atlas texture: either provided by asset texture, or generated as a runtime color atlas.
            Texture2D atlasTex = asset.atlasTexture;
            if (atlasTex == null)
            {
                atlasTex = TryBuildRuntimeColorAtlas(asset);
                if (atlasTex != null)
                    _ownedTextures.Add(atlasTex);
            }

            // Material instancing policy: one instance per (world,key).
            Material template = entry.materialTemplateOverride != null
                ? entry.materialTemplateOverride
                : asset.atlasMaterial;

            // If authoring didn't generate a material, create a runtime template.
            // This keeps the renderer strict (it still gets a Material), but avoids hard failure.
            if (template == null)
            {
                Shader shader =
                    Shader.Find("WorldGrid/Unlit Vertex Tint Blend") ?? // if you added it
                    Shader.Find("Sprites/Default") ??
                    Shader.Find("Unlit/Transparent") ??
                    Shader.Find("Unlit/Texture");

                if (shader == null)
                    throw new InvalidOperationException(
                        $"TileLibraryProvider: key '{key.Value}' has no material template and no fallback shader was found.");

                template = new Material(shader)
                {
                    name = $"WorldGrid_RuntimeTemplate_{key.Value}"
                };

                _ownedMaterials.Add(template);
            }

            Material instanced = TileMaterialFactory.CreateInstance(template, atlasTex);
            if (instanced != null) _ownedMaterials.Add(instanced);

            var view = new TileLibraryView(key, runtimeLib, atlasTex, instanced);
            _views[key.Value] = view;
            return view;
        }

        private static Texture2D TryBuildRuntimeColorAtlas(TileLibraryAsset asset)
        {
            // We can only build a deterministic atlas that matches UVs if the asset has an effective atlas size.
            if (!asset.TryGetEffectiveAtlasSize(out int atlasW, out int atlasH))
                return null;

            if (atlasW <= 0 || atlasH <= 0)
                return null;

            // Create runtime texture
            var tex = new Texture2D(atlasW, atlasH, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"{asset.name}_RuntimeColorAtlas"
            };

            // Fill transparent background
            var pixels = new Color32[atlasW * atlasH];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);

            // Paint each entry's tile rect in pixels to match ComputeUvFromTileCoord math.
            var tileSize = asset.tilePixelSize;
            var pad = asset.paddingPixels;

            int stepX = tileSize.x + pad.x;
            int stepY = tileSize.y + pad.y;

            for (int i = 0; i < asset.entries.Count; i++)
            {
                var e = asset.entries[i];
                if (e == null) continue;

                int x0 = e.tileCoord.x * stepX;
                int y0 = e.tileCoord.y * stepY;

                // Mirror the asset's UV computation: originTopLeft means y grows downward in coords.
                if (asset.originTopLeft)
                    y0 = atlasH - y0 - tileSize.y;

                int w = e.tileSpan.x * tileSize.x + (e.tileSpan.x - 1) * pad.x;
                int h = e.tileSpan.y * tileSize.y + (e.tileSpan.y - 1) * pad.y;

                // Clamp to atlas
                int x1 = Mathf.Clamp(x0 + w, 0, atlasW);
                int y1 = Mathf.Clamp(y0 + h, 0, atlasH);
                x0 = Mathf.Clamp(x0, 0, atlasW);
                y0 = Mathf.Clamp(y0, 0, atlasH);

                var c = e.color;

                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                        pixels[y * atlasW + x] = c;
            }

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return tex;
        }

        private bool TryGetEntry(TileLibraryKey key, out TileLibraryProviderEntry entry)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null) continue;
                if (e.key.Equals(key))
                {
                    entry = e;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        private void OnDestroy()
        {
            // Provider owns instanced materials (per world lifetime).
            for (int i = 0; i < _ownedMaterials.Count; i++)
            {
                var m = _ownedMaterials[i];
                if (m == null) continue;

#if UNITY_EDITOR
                DestroyImmediate(m);
#else
                Destroy(m);
#endif
            }

            _ownedMaterials.Clear();

            // NEW: destroy runtime textures
            for (int i = 0; i < _ownedTextures.Count; i++)
            {
                var t = _ownedTextures[i];
                if (t == null) continue;

#if UNITY_EDITOR
                DestroyImmediate(t);
#else
                Destroy(t);
#endif
            }

            _ownedTextures.Clear();
            _views.Clear();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null) continue;

                if (e.key.IsEmpty)
                    Debug.LogWarning($"{name}: TileLibraryProvider entry {i} has an empty key.", this);

                if (e.asset == null)
                    Debug.LogWarning($"{name}: TileLibraryProvider entry '{e.key}' has no asset.", this);

                if (!e.key.IsEmpty && !seen.Add(e.key.Value))
                    Debug.LogWarning($"{name}: TileLibraryProvider has a duplicate key '{e.key.Value}'.", this);
            }
        }
#endif
    }
}
