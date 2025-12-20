using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Animation
{
    [CreateAssetMenu(menuName = "Animation/Animation Clip Def", fileName = "NewAnimationClipDef")]
    public sealed class AnimationClipDef : ScriptableObject
    {
        public enum SourceMode
        {
            ManualFrames = 0,
            UnityAnimationClip = 1
        }

        [Header("Source")]
        public SourceMode source = SourceMode.ManualFrames;

        [Header("Manual Sprite Animation (MVP)")]
        public Sprite[] frames;

        [Header("Unity AnimationClip Source (Sprite Swap)")]
        [Tooltip("AnimationClip that animates SpriteRenderer.sprite via sprite swap keyframes.")]
        public AnimationClip unityClip;

        [Tooltip("If true, bake frames from the Unity AnimationClip into a Sprite[] (recommended).")]
        public bool preferBakedFrames = true;

        [Tooltip("Baked frames extracted from unityClip (optional, but recommended for builds/perf).")]
        public Sprite[] bakedFrames;

        [Min(0.01f)]
        public float fps = 12f;

        public bool loop = true;

        [Tooltip("If true, restarting the same clip resets to frame 0.")]
        public bool restartOnEnter = true;

        // -------------------------
        // Runtime cache (non-serialized)
        // -------------------------
        [System.NonSerialized] private Sprite[] _runtimeFrames;
        [System.NonSerialized] private int _runtimeId;
        [System.NonSerialized] private bool _runtimeResolved;

        public bool HasFrames
        {
            get
            {
                var f = GetResolvedFrames();
                return f != null && f.Length > 0;
            }
        }

        /// <summary>
        /// Editor-time auto-selection of source mode.
        /// - If ONLY unityClip is set => UnityAnimationClip
        /// - If ONLY frames are set   => ManualFrames
        /// - If BOTH are set          => keep user selection (do not fight authoring)
        /// - If NEITHER set           => keep ManualFrames default
        /// </summary>
        private void OnValidate()
        {
            bool hasManual = frames != null && frames.Length > 0;
            bool hasUnity = unityClip != null;

            // If only one source is present, snap the dropdown to match.
            if (hasUnity && !hasManual)
            {
                if (source != SourceMode.UnityAnimationClip)
                    source = SourceMode.UnityAnimationClip;
            }
            else if (hasManual && !hasUnity)
            {
                if (source != SourceMode.ManualFrames)
                    source = SourceMode.ManualFrames;
            }
            // If both are present, DO NOT override the author’s explicit selection.
            // If neither present, keep whatever (default is ManualFrames).

            // If source says Unity but unityClip is null (e.g. someone cleared it),
            // fall back to manual if available.
            if (source == SourceMode.UnityAnimationClip && unityClip == null && hasManual)
                source = SourceMode.ManualFrames;

            // Keep runtime resolution from becoming stale in playmode/editor.
            InvalidateRuntimeCache();
        }

        private void InvalidateRuntimeCache()
        {
            _runtimeResolved = false;
            _runtimeFrames = null;
            _runtimeId = 0;
        }

        public int GetStableId()
        {
            // Use clip identity if available so “same clip” checks remain stable across resolutions.
            if (source == SourceMode.UnityAnimationClip && unityClip != null)
                return unityClip.GetInstanceID();

            return GetInstanceID();
        }

        public float GetResolvedFps()
        {
            if (source == SourceMode.UnityAnimationClip && unityClip != null)
            {
                // Prefer clip frameRate if it’s sensible.
                if (unityClip.frameRate > 0.01f) return unityClip.frameRate;
            }
            return Mathf.Max(0.01f, fps);
        }

        public Sprite[] GetResolvedFrames()
        {
            if (_runtimeResolved && _runtimeFrames != null && _runtimeFrames.Length > 0)
                return _runtimeFrames;

            _runtimeResolved = true;
            _runtimeId = GetStableId();

            // 1) Manual
            if (source == SourceMode.ManualFrames)
            {
                _runtimeFrames = frames;
                return _runtimeFrames;
            }

            // 2) Unity clip
            if (unityClip == null)
            {
                _runtimeFrames = null;
                return null;
            }

            // Prefer baked frames if requested and present
            if (preferBakedFrames && bakedFrames != null && bakedFrames.Length > 0)
            {
                _runtimeFrames = bakedFrames;
                return _runtimeFrames;
            }

#if UNITY_EDITOR
            // In editor, we can extract sprite keyframes without sampling.
            _runtimeFrames = ExtractSpritesFromClip_Editor(unityClip);
            return _runtimeFrames;
#else
            // In builds, you should rely on bakedFrames.
            _runtimeFrames = null;
            return null;
#endif
        }

#if UNITY_EDITOR
        private static Sprite[] ExtractSpritesFromClip_Editor(AnimationClip clip)
        {
            if (clip == null) return null;

            // Find any object reference curves that drive SpriteRenderer.sprite
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (b.type != typeof(SpriteRenderer)) continue;
                if (b.propertyName != "m_Sprite") continue;

                var keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
                if (keys == null || keys.Length == 0) return null;

                var result = new Sprite[keys.Length];
                for (int k = 0; k < keys.Length; k++)
                    result[k] = keys[k].value as Sprite;

                return result;
            }

            return null;
        }

        [ContextMenu("Bake From Unity Clip")]
        private void BakeFromUnityClip_ContextMenu()
        {
            if (unityClip == null)
            {
                Debug.LogWarning($"[{name}] No unityClip assigned.");
                return;
            }

            var extracted = ExtractSpritesFromClip_Editor(unityClip);
            if (extracted == null || extracted.Length == 0)
            {
                Debug.LogWarning($"[{name}] Could not extract SpriteRenderer.sprite keys from clip '{unityClip.name}'. " +
                                 $"Make sure it is a sprite-swap clip animating SpriteRenderer.sprite.");
                return;
            }

            bakedFrames = extracted;

            // Reasonable defaults:
            if (fps <= 0.01f && unityClip.frameRate > 0.01f)
                fps = unityClip.frameRate;

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();

            Debug.Log($"[{name}] Baked {bakedFrames.Length} sprites from '{unityClip.name}'.");
        }
#endif
    }
}
