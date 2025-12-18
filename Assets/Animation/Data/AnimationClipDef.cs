using UnityEngine;

namespace Animation
{
    [CreateAssetMenu(menuName = "Animation/Animation Clip Def", fileName = "NewAnimationClipDef")]
    public sealed class AnimationClipDef : ScriptableObject
    {
        [Header("Manual Sprite Animation (MVP)")]
        public Sprite[] frames;

        [Min(0.01f)]
        public float fps = 12f;

        public bool loop = true;

        [Tooltip("If true, restarting the same clip resets to frame 0.")]
        public bool restartOnEnter = true;

        public bool HasFrames => frames != null && frames.Length > 0;
    }
}
