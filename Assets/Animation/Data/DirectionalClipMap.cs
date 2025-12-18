using UnityEngine;

namespace Animation
{
    [System.Serializable]
    public class DirectionalClipMap
    {
        public DirectionMode mode = DirectionMode.One;

        [Tooltip("If a direction-specific clip is missing, allow using an opposite clip with mirroring (warn once at runtime).")]
        public bool allowMirrorFallback = true;

        [Header("Mode: One")]
        public AnimationClipDef one;

        [Header("Mode: Two")]
        public AnimationClipDef left;
        public AnimationClipDef right;

        [Header("Mode: Four")]
        public AnimationClipDef up;
        public AnimationClipDef down;

        public bool TryResolve(bool facingRight, out AnimationClipDef clip, out MirrorInstruction mirror)
        {
            clip = null;
            mirror = MirrorInstruction.None;

            switch (mode)
            {
                case DirectionMode.One:
                    clip = one;
                    mirror = facingRight ? MirrorInstruction.Horizontal : MirrorInstruction.None;
                    return clip != null;

                case DirectionMode.Two:
                    if (!facingRight)
                    {
                        clip = left;
                        if (clip != null) return true;

                        if (allowMirrorFallback && right != null)
                        {
                            clip = right;
                            mirror = MirrorInstruction.Horizontal;
                            return true;
                        }
                        return false;
                    }
                    else
                    {
                        clip = right;
                        if (clip != null) return true;

                        if (allowMirrorFallback && left != null)
                        {
                            clip = left;
                            mirror = MirrorInstruction.Horizontal;
                            return true;
                        }
                        return false;
                    }

                default:
                    return false;
            }
        }
    }
}
