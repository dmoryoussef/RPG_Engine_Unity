using UnityEngine;

namespace Animation
{
    [System.Serializable]
    public class DirectionalVisualSet
    {
        public DirectionMode mode = DirectionMode.One;

        [Tooltip("If a direction-specific clip is missing, allow using an opposite clip with mirroring (warn once at runtime).")]
        public bool allowMirrorFallback = true;

        [Header("Mode: One")]
        public AnimationClipDef one;

        [Tooltip("If true, the 'one' clip is authored facing RIGHT. If false, it's authored facing LEFT.")]
        public bool oneFacesRight = true;

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
                    if (clip == null) return false;

                    // If the source faces RIGHT, mirror for LEFT.
                    // If the source faces LEFT, mirror for RIGHT.
                    bool shouldMirror = oneFacesRight ? !facingRight : facingRight;
                    mirror = shouldMirror ? MirrorInstruction.Horizontal : MirrorInstruction.None;
                    return true;

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
