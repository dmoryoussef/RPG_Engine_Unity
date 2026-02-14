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
        [SerializeField] private bool debugTilesetBuild = true;

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
            // Editor-only lightweight check; runtime does authoritative validation in OnEnable.
            if (entries == null || entries.Count == 0)
                return;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null)
                    continue;

                if (e.key.IsEmpty)
                    continue;

                if (!seen.Add(e.key.Value))
                {
                    UnityEngine.Debug.LogWarning($"{name}: Duplicate TileLibraryKey '{e.key.Value}' in entries (first wins at runtime).", this);
                }
            }
        }
#endif

        private void warnOnce(string token, string message)
        {
            if (_warned.Contains(token))
                return;

            _warned.Add(token);
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

            var asset = entry != null ? entry.asset : null;
            if (asset == null)
            {
                error = $"TileLibraryProvider: missing TileLibraryAsset for key '{key.Value}'.";
                if (debugTilesetBuild) UnityEngine.Debug.LogError(error);
                return false;
            }

            if (debugTilesetBuild)
            {
                UnityEngine.Debug.Log(
                    $"[TileProvider] BuildView BEGIN key='{key.Value}' asset='{asset.name}' " +
                    $"assetTemplateMat={(asset.templateMaterial != null ? asset.templateMaterial.name : "NULL")} " +
                    $"assetTemplateShader={(asset.templateMaterial != null ? asset.templateMaterial.shader.name : "NULL")}");
            }

            // 1) Build runtime library
            TileLibrary runtimeLib;
            try
            {
                runtimeLib = asset.BuildRuntime();
            }
            catch (Exception ex)
            {
                error = $"TileLibraryProvider: failed to build runtime library for key '{key.Value}': {ex.Message}";
                if (debugTilesetBuild) UnityEngine.Debug.LogError($"[TileProvider] BuildRuntime FAILED key='{key.Value}' :: {ex}");
                return false;
            }

            if (runtimeLib == null)
            {
                error = $"TileLibraryProvider: BuildRuntime returned null for key '{key.Value}' (asset '{asset.name}').";
                if (debugTilesetBuild) UnityEngine.Debug.LogError(error);
                return false;
            }

            // 2) Resolve atlas texture
            var atlasTex = resolveAtlasTexture(asset);
            if (debugTilesetBuild)
            {
                UnityEngine.Debug.Log(
                    $"[TileProvider] AtlasResolved key='{key.Value}' atlasTex={(atlasTex != null ? atlasTex.name : "NULL")}");
            }

            // 3) Resolve template material
            // Priority:
            //  a) entry override template (resolveMaterialTemplate(entry))
            //  b) asset.templateMaterial
            //  c) provider fallback template created by tryEnsureTemplateMaterial
            var template = resolveMaterialTemplate(entry);

            if (template == null && asset.templateMaterial != null)
                template = asset.templateMaterial;

            if (debugTilesetBuild)
            {
                UnityEngine.Debug.Log(
                    $"[TileProvider] TemplateResolved key='{key.Value}' " +
                    $"templateMat={(template != null ? template.name : "NULL")} " +
                    $"templateShader={(template != null ? template.shader.name : "NULL")}");
            }

            // Ensure we have a valid template (creates fallback template if needed)
            if (!tryEnsureTemplateMaterial(key, ref template, out error))
            {
                if (debugTilesetBuild) UnityEngine.Debug.LogError($"[TileProvider] EnsureTemplate FAILED key='{key.Value}' :: {error}");
                return false;
            }

            if (template == null)
            {
                error = $"TileLibraryProvider: template material is null after tryEnsureTemplateMaterial for key '{key.Value}'.";
                if (debugTilesetBuild) UnityEngine.Debug.LogError(error);
                return false;
            }

            // 4) Instantiate provider-owned runtime material
            Material instanced = null;
            try
            {
                instanced = UnityEngine.Object.Instantiate(template);
            }
            catch (Exception ex)
            {
                error = $"TileLibraryProvider: failed to instantiate template material for key '{key.Value}': {ex.Message}";
                if (debugTilesetBuild) UnityEngine.Debug.LogError($"[TileProvider] Instantiate FAILED key='{key.Value}' :: {ex}");
                return false;
            }

            if (instanced == null)
            {
                error = $"TileLibraryProvider: Instantiate returned null material for key '{key.Value}'.";
                if (debugTilesetBuild) UnityEngine.Debug.LogError(error);
                return false;
            }

            _ownedMaterials.Add(instanced);
            instanced.renderQueue = 2450;

            // 5) Bind atlas texture to common properties
            if (atlasTex != null)
            {
                bool bound = false;

                if (instanced.HasProperty("_BaseMap"))
                {
                    instanced.SetTexture("_BaseMap", atlasTex);
                    bound = true;
                }

                if (instanced.HasProperty("_MainTex"))
                {
                    instanced.SetTexture("_MainTex", atlasTex);
                    bound = true;
                }

                if (!bound)
                {
                    // last resort
                    instanced.mainTexture = atlasTex;
                }
            }

            // 6) Optional bind height atlas (if you're still carrying it; harmless if shader doesn't use it)
            if (asset.heightAtlasTexture != null && instanced.HasProperty("_HeightTex"))
                instanced.SetTexture("_HeightTex", asset.heightAtlasTexture);

            // 7) Final debug snapshot of the instantiated material
            if (debugTilesetBuild)
            {
                var instShader = instanced.shader != null ? instanced.shader.name : "NULL";
                var instMainTex = instanced.mainTexture != null ? instanced.mainTexture.name : "NULL";

                float rs = instanced.HasProperty("_ReliefStrength") ? instanced.GetFloat("_ReliefStrength") : -999f;
                float amb = instanced.HasProperty("_Ambient") ? instanced.GetFloat("_Ambient") : -999f;
                float ns = instanced.HasProperty("_NoiseScale") ? instanced.GetFloat("_NoiseScale") : -999f;

                UnityEngine.Debug.Log(
                    $"[TileProvider] BuildView OK key='{key.Value}' instMat='{instanced.name}' " +
                    $"shader='{instShader}' mainTex='{instMainTex}' " +
                    $"hasRelief={(instanced.HasProperty("_ReliefStrength"))} " +
                    $"_ReliefStrength={rs} _Ambient={amb} _NoiseScale={ns}");
            }

            // 8) Create view and cache it
            var built = new TileLibraryView(key, runtimeLib, atlasTex, instanced);
            _views[key.Value] = built;
            view = built;

            if (debugTilesetBuild)
                UnityEngine.Debug.Log($"[TileProvider] BuildView END key='{key.Value}' viewCached=true");

            return true;
        }

        private Texture2D resolveHeightAtlasTexture(TileLibraryAsset asset)
        {
            if (asset == null)
                return null;

            // If you added: public Texture2D heightAtlasTexture;
            return asset.heightAtlasTexture;
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

        private static Material resolveMaterialTemplate(TileLibraryProviderEntry entry)
        {
            // TileLibraryAsset no longer owns materials/shaders. Provider may optionally use an override template,
            // otherwise it will create a runtime template using a fallback shader.
            return entry.materialTemplateOverride;
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
                name = $"WorldGrid_RuntimeColorAtlas_{asset.name}"
            };

            // Fill with white (placeholder). Extend later to bake per-tile base colors into an atlas if desired.
            var pixels = new Color32[atlasW * atlasH];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            return tex;
        }

        #endregion

        #region Cleanup

        private void cleanupOwnedResources()
        {
            // Destroy owned materials
            for (int i = 0; i < _ownedMaterials.Count; i++)
            {
                var m = _ownedMaterials[i];
                if (m != null)
                    Destroy(m);
            }
            _ownedMaterials.Clear();

            // Destroy owned textures
            for (int i = 0; i < _ownedTextures.Count; i++)
            {
                var t = _ownedTextures[i];
                if (t != null)
                    Destroy(t);
            }
            _ownedTextures.Clear();

            _views.Clear();
            _warned.Clear();
        }

        #endregion
    }
}
