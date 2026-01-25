using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGrid.Unity.Assets;
using WorldGrid.Runtime.Tiles;

namespace WorldGrid.Unity.Rendering
{
    /// <summary>
    /// World-level authority for resolving tile libraries by key.
    ///
    /// Responsibilities:
    /// - Validates entries at runtime (including duplicate key detection)
    /// - Builds runtime TileLibrary from authoring assets
    /// - Instantiates runtime materials (per world, per key)
    /// - Optionally builds a runtime color atlas when no atlas texture is authored
    /// - Owns and destroys runtime-created materials and textures
    ///
    /// Lifecycle:
    /// - Validates and becomes ready in OnEnable
    /// - Cleans up owned resources in OnDisable and OnDestroy (idempotent)
    /// </summary>
    public sealed class TileLibraryProvider : MonoBehaviour, ITileLibrarySource
    {
        #region Inspector

        [SerializeField]
        private List<TileLibraryProviderEntry> entries = new();

        #endregion

        #region State

        // Key string -> validated entry (unique, deterministic)
        private readonly Dictionary<string, TileLibraryProviderEntry> _entryMap = new(StringComparer.Ordinal);

        // Key string -> view (stable per provider lifetime while enabled)
        private readonly Dictionary<string, ITileLibraryView> _views = new(StringComparer.Ordinal);

        // Runtime resources owned by this provider instance
        private readonly List<Material> _ownedMaterials = new();
        private readonly List<Texture2D> _ownedTextures = new();

        // Warn-once guard (per provider instance)
        private readonly HashSet<string> _warned = new(StringComparer.Ordinal);

        public bool IsReady { get; private set; }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            validateEntriesRuntime();
            IsReady = true;
        }

        private void OnDisable()
        {
            cleanupOwnedResources();
            IsReady = false;
        }

        private void OnDestroy()
        {
            cleanupOwnedResources();
            _entryMap.Clear();
            IsReady = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            validateEntriesEditor();
        }
#endif

        #endregion

        #region Public API

        public bool Has(TileLibraryKey key)
        {
            return IsReady && !key.IsEmpty && _entryMap.ContainsKey(key.Value);
        }

        /// <summary>
        /// Runtime-safe view resolution. Never throws.
        /// </summary>
        public bool TryGet(TileLibraryKey key, out ITileLibraryView view, out string error)
        {
            view = null;
            error = null;

            if (!IsReady)
            {
                error = "TileLibraryProvider is not ready (provider is disabled or OnEnable has not run).";
                return false;
            }

            if (key.IsEmpty)
            {
                error = "TileLibraryKey is empty.";
                return false;
            }

            if (_views.TryGetValue(key.Value, out var cached) && cached != null)
            {
                view = cached;
                return true;
            }

            if (!_entryMap.TryGetValue(key.Value, out var entry) || entry == null || entry.asset == null)
            {
                error = $"TileLibraryProvider: missing key '{key.Value}'.";
                return false;
            }

            return tryBuildView(key, entry, out view, out error);
        }

        /// <summary>
        /// Convenience accessor.
        /// In Editor/Development builds, throws when resolution fails to surface wiring issues early.
        /// In Release builds, logs once and returns null.
        /// </summary>
        public ITileLibraryView Get(TileLibraryKey key)
        {
            if (TryGet(key, out var view, out var error))
                return view;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            throw new InvalidOperationException(error);
#else
            warnOnce($"get_fail::{key.Value}", error);
            return null;
#endif
        }

        #endregion

        #region Validation

        private void validateEntriesRuntime()
        {
            _entryMap.Clear();

            if (entries == null || entries.Count == 0)
            {
                warnOnce("no_entries", $"{name}: TileLibraryProvider has no entries.");
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null)
                    continue;

                if (e.key.IsEmpty)
                {
                    warnOnce($"empty_key::{i}", $"{name}: TileLibraryProvider entry {i} has an empty key (ignored).");
                    continue;
                }

                if (e.asset == null)
                {
                    warnOnce($"missing_asset::{e.key.Value}", $"{name}: TileLibraryProvider entry '{e.key}' has no asset (ignored).");
                    continue;
                }

                // Deterministic duplicate handling: first wins.
                if (_entryMap.ContainsKey(e.key.Value))
                {
                    warnOnce(
                        $"dup_key::{e.key.Value}",
                        $"{name}: TileLibraryProvider has duplicate key '{e.key.Value}'. Using first entry and ignoring later duplicates.");
                    continue;
                }

                _entryMap.Add(e.key.Value, e);
            }
        }

#if UNITY_EDITOR
        private void validateEntriesEditor()
        {
            if (entries == null)
                return;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null)
                    continue;

                if (e.key.IsEmpty)
                    UnityEngine.Debug.LogWarning($"{name}: TileLibraryProvider entry {i} has an empty key.", this);

                if (e.asset == null)
                    UnityEngine.Debug.LogWarning($"{name}: TileLibraryProvider entry '{e.key}' has no asset.", this);

                if (!e.key.IsEmpty && !seen.Add(e.key.Value))
                    UnityEngine.Debug.LogWarning($"{name}: TileLibraryProvider has a duplicate key '{e.key.Value}'.", this);
            }
        }
#endif

        private void warnOnce(string key, string message)
        {
            if (_warned.Add(key))
                UnityEngine.Debug.LogWarning(message, this);
        }

        #endregion

        #region View Build

        private bool tryBuildView(
            TileLibraryKey key,
            TileLibraryProviderEntry entry,
            out ITileLibraryView view,
            out string error)
        {
            view = null;
            error = null;

            var asset = entry.asset;

            TileLibrary runtimeLib;
            try
            {
                runtimeLib = asset.BuildRuntime();
            }
            catch (Exception ex)
            {
                error = $"TileLibraryProvider: failed to build runtime library for key '{key.Value}': {ex.Message}";
                return false;
            }

            var atlasTex = resolveAtlasTexture(asset);
            var template = resolveMaterialTemplate(entry, asset);
            if (!tryEnsureTemplateMaterial(key, ref template, out error))
                return false;

            var instanced = TileMaterialFactory.CreateInstance(template, atlasTex);
            if (instanced != null)
                _ownedMaterials.Add(instanced);
            if (instanced != null) instanced.renderQueue = 2450; // or 2450


            var built = new TileLibraryView(key, runtimeLib, atlasTex, instanced);
            _views[key.Value] = built;
            view = built;
            return true;
        }

        private Texture2D resolveAtlasTexture(TileLibraryAsset asset)
        {
            var atlasTex = asset.atlasTexture;
            if (atlasTex != null)
                return atlasTex;

            var runtimeAtlas = tryBuildRuntimeColorAtlas(asset);
            if (runtimeAtlas != null)
                _ownedTextures.Add(runtimeAtlas);

            return runtimeAtlas;
        }

        private static Material resolveMaterialTemplate(TileLibraryProviderEntry entry, TileLibraryAsset asset)
        {
            return entry.materialTemplateOverride != null
                ? entry.materialTemplateOverride
                : asset.atlasMaterial;
        }

        private bool tryEnsureTemplateMaterial(TileLibraryKey key, ref Material template, out string error)
        {
            error = null;

            if (template != null)
                return true;

            var shader = findFallbackShader();
            if (shader == null)
            {
                error = $"TileLibraryProvider: key '{key.Value}' has no material template and no fallback shader was found.";
                return false;
            }

            // Provider-created runtime template is owned and destroyed by provider.
            template = new Material(shader)
            {
                name = $"WorldGrid_RuntimeTemplate_{key.Value}"
            };
            _ownedMaterials.Add(template);
            return true;
        }

        private static Shader findFallbackShader()
        {
            return Shader.Find("WorldGrid/Unlit Vertex Tint Blend")
                   ?? Shader.Find("Sprites/Default")
                   ?? Shader.Find("Unlit/Transparent")
                   ?? Shader.Find("Unlit/Texture");
        }

        #endregion

        #region Runtime Atlas (Optional Fallback)

        private static Texture2D tryBuildRuntimeColorAtlas(TileLibraryAsset asset)
        {
            if (!asset.TryGetEffectiveAtlasSize(out int atlasW, out int atlasH))
                return null;

            if (atlasW <= 0 || atlasH <= 0)
                return null;

            var tex = new Texture2D(atlasW, atlasH, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"{asset.name}_RuntimeColorAtlas"
            };

            var pixels = new Color32[atlasW * atlasH];
            fill(pixels, new Color32(0, 0, 0, 0));

            paintEntries(asset, atlasW, atlasH, pixels);

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return tex;
        }

        private static void fill(Color32[] pixels, Color32 c)
        {
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = c;
        }

        private static void paintEntries(TileLibraryAsset asset, int atlasW, int atlasH, Color32[] pixels)
        {
            var tileSize = asset.tilePixelSize;
            var pad = asset.paddingPixels;

            int stepX = tileSize.x + pad.x;
            int stepY = tileSize.y + pad.y;

            for (int i = 0; i < asset.entries.Count; i++)
            {
                var e = asset.entries[i];
                if (e == null)
                    continue;

                int x0 = e.tileCoord.x * stepX;
                int y0 = e.tileCoord.y * stepY;

                if (asset.originTopLeft)
                    y0 = atlasH - y0 - tileSize.y;

                int w = e.tileSpan.x * tileSize.x + (e.tileSpan.x - 1) * pad.x;
                int h = e.tileSpan.y * tileSize.y + (e.tileSpan.y - 1) * pad.y;

                clampRect(ref x0, ref y0, ref w, ref h, atlasW, atlasH);
                if (w <= 0 || h <= 0)
                    continue;

                paintRect(pixels, atlasW, x0, y0, w, h, e.color);
            }
        }

        private static void clampRect(ref int x0, ref int y0, ref int w, ref int h, int atlasW, int atlasH)
        {
            int x1 = x0 + w;
            int y1 = y0 + h;

            x0 = Mathf.Clamp(x0, 0, atlasW);
            y0 = Mathf.Clamp(y0, 0, atlasH);
            x1 = Mathf.Clamp(x1, 0, atlasW);
            y1 = Mathf.Clamp(y1, 0, atlasH);

            w = x1 - x0;
            h = y1 - y0;
        }

        private static void paintRect(Color32[] pixels, int atlasW, int x0, int y0, int w, int h, Color32 c)
        {
            int x1 = x0 + w;
            int y1 = y0 + h;

            for (int y = y0; y < y1; y++)
            {
                int row = y * atlasW;
                for (int x = x0; x < x1; x++)
                    pixels[row + x] = c;
            }
        }

        #endregion

        #region Cleanup

        private void cleanupOwnedResources()
        {
            // Clear views first so consumers do not retain stale references across disable/enable.
            _views.Clear();

            destroyOwnedMaterials();
            destroyOwnedTextures();
        }

        private void destroyOwnedMaterials()
        {
            for (int i = 0; i < _ownedMaterials.Count; i++)
            {
                var m = _ownedMaterials[i];
                if (m == null)
                    continue;

#if UNITY_EDITOR
                DestroyImmediate(m);
#else
                Destroy(m);
#endif
            }

            _ownedMaterials.Clear();
        }

        private void destroyOwnedTextures()
        {
            for (int i = 0; i < _ownedTextures.Count; i++)
            {
                var t = _ownedTextures[i];
                if (t == null)
                    continue;

#if UNITY_EDITOR
                DestroyImmediate(t);
#else
                Destroy(t);
#endif
            }

            _ownedTextures.Clear();
        }

        #endregion
    }
}
