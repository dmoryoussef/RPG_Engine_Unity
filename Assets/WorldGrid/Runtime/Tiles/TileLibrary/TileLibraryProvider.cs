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

            // Phase 1: runtime library is built from the asset.
            TileLibrary runtimeLib = asset.BuildRuntime();

            // Phase 1: atlas texture is supplied by the asset (spritesheet or asset-owned procedural).
            Texture2D atlasTex = asset.atlasTexture;

            // Material instancing policy: one instance per (world,key).
            Material template = entry.materialTemplateOverride != null
                ? entry.materialTemplateOverride
                : asset.atlasMaterial;

            if (template == null)
            {
                // Keep this strict for now because the renderer requires a material today.
                throw new InvalidOperationException(
                    $"TileLibraryProvider: key '{key.Value}' has no material template (asset.atlasMaterial is null and no override is set).");
            }

            Material instanced = TileMaterialFactory.CreateInstance(template, atlasTex);
            if (instanced != null) _ownedMaterials.Add(instanced);

            var view = new TileLibraryView(key, runtimeLib, atlasTex, instanced);
            _views[key.Value] = view;
            return view;
        }

        private bool TryGetEntry(TileLibraryKey key, out TileLibraryProviderEntry entry)
        {
            // Keep it simple for now: linear scan over a small list.
            // If this grows, we can build an index in Awake/OnValidate.
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
            _views.Clear();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Optional: early warnings to avoid silent runtime surprises.
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
