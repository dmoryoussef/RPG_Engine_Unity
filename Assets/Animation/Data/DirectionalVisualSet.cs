using UnityEngine;

namespace Animation
{
    [System.Serializable]
    public sealed class DirectionalVisualSet
    {
        public DirectionMode mode = DirectionMode.One;

        [Tooltip("If a direction-specific clip is missing, allow using mirrored fallbacks.")]
        public bool allowMirrorFallback = true;

        [Header("Mode: One")]
        public AnimationClipDef one;

        [Tooltip("If true, the 'one' clip is authored facing RIGHT. If false, authored facing LEFT.")]
        public bool oneFacesRight = true;

        [Header("Mode: Two")]
        public AnimationClipDef left;
        public AnimationClipDef right;

        [Header("Mode: Four")]
        public AnimationClipDef up;
        public AnimationClipDef down;

        [Header("Mode: Eight (Diagonals)")]
        public AnimationClipDef upLeft;
        public AnimationClipDef upRight;
        public AnimationClipDef downLeft;
        public AnimationClipDef downRight;

        public readonly struct ResolvedVisual
        {
            public readonly AnimationClipDef clip;
            public readonly bool mirrorX;
            public readonly bool mirrorY;

            public ResolvedVisual(AnimationClipDef clip, bool mirrorX, bool mirrorY)
            {
                this.clip = clip;
                this.mirrorX = mirrorX;
                this.mirrorY = mirrorY;
            }
        }

        public bool TryGetVisualForDirection(Vector2 facing, out ResolvedVisual visual)
        {
            visual = default;

            if (facing.sqrMagnitude < 0.0001f)
                facing = Vector2.right;

            switch (mode)
            {
                case DirectionMode.One:
                    return TryGet_One(facing, out visual);

                case DirectionMode.Two:
                    return TryGet_Two(facing, out visual);

                case DirectionMode.Four:
                    return TryGet_Four(facing, out visual);

                case DirectionMode.Eight:
                    return TryGet_Eight(facing, out visual);

                default:
                    return false;
            }
        }

        // -------------------------
        // One
        // -------------------------
        private bool TryGet_One(Vector2 facing, out ResolvedVisual visual)
        {
            visual = default;
            if (one == null) return false;

            bool facingRight = facing.x >= 0f;
            bool mirrorX = oneFacesRight ? !facingRight : facingRight;
            visual = new ResolvedVisual(one, mirrorX, mirrorY: false);
            return true;
        }

        // -------------------------
        // Two
        // -------------------------
        private bool TryGet_Two(Vector2 facing, out ResolvedVisual visual)
        {
            visual = default;

            bool facingRight = facing.x >= 0f;

            if (facingRight)
            {
                if (right != null)
                {
                    visual = new ResolvedVisual(right, mirrorX: false, mirrorY: false);
                    return true;
                }

                if (allowMirrorFallback && left != null)
                {
                    visual = new ResolvedVisual(left, mirrorX: true, mirrorY: false);
                    return true;
                }

                return false;
            }

            // facing left
            if (left != null)
            {
                visual = new ResolvedVisual(left, mirrorX: false, mirrorY: false);
                return true;
            }

            if (allowMirrorFallback && right != null)
            {
                visual = new ResolvedVisual(right, mirrorX: true, mirrorY: false);
                return true;
            }

            return false;
        }

        // -------------------------
        // Four
        // -------------------------
        private bool TryGet_Four(Vector2 facing, out ResolvedVisual visual)
        {
            visual = default;

            Dir8 d = QuantizeTo4(facing);

            if (TryGetExact(d, out var clip))
            {
                visual = new ResolvedVisual(clip, mirrorX: false, mirrorY: false);
                return true;
            }

            if (!allowMirrorFallback)
                return false;

            // Opposite direction fallback via mirroring
            var (opp, mx, my) = OppositeWithMirror(d);

            if (TryGetExact(opp, out clip))
            {
                visual = new ResolvedVisual(clip, mx, my);
                return true;
            }

            return false;
        }

        // -------------------------
        // Eight
        // -------------------------
        private bool TryGet_Eight(Vector2 facing, out ResolvedVisual visual)
        {
            visual = default;

            Dir8 d = QuantizeTo8(facing);

            // Exact
            if (TryGetExact(d, out var clip))
            {
                visual = new ResolvedVisual(clip, mirrorX: false, mirrorY: false);
                return true;
            }

            if (!allowMirrorFallback)
                return false;

            // Fallback order (cheap + intuitive):
            // 1) mirrorX version
            // 2) mirrorY version
            // 3) mirrorX+mirrorY version
            // 4) nearest cardinal (dominant axis)
            // 5) opposite cardinal (with mirror)

            var (mxDir, mxX, mxY) = MirrorX(d);
            if (TryGetExact(mxDir, out clip))
            {
                visual = new ResolvedVisual(clip, mxX, mxY);
                return true;
            }

            var (myDir, myX, myY) = MirrorY(d);
            if (TryGetExact(myDir, out clip))
            {
                visual = new ResolvedVisual(clip, myX, myY);
                return true;
            }

            var (mbDir, mbX, mbY) = MirrorXY(d);
            if (TryGetExact(mbDir, out clip))
            {
                visual = new ResolvedVisual(clip, mbX, mbY);
                return true;
            }

            Dir8 cardinal = ToNearestCardinal(d);
            if (TryGetExact(cardinal, out clip))
            {
                visual = new ResolvedVisual(clip, mirrorX: false, mirrorY: false);
                return true;
            }

            var (oppCard, ox, oy) = OppositeWithMirror(cardinal);
            if (TryGetExact(oppCard, out clip))
            {
                visual = new ResolvedVisual(clip, ox, oy);
                return true;
            }

            return false;
        }

        // -------------------------
        // Direction helpers
        // -------------------------

        private enum Dir8
        {
            Right,
            UpRight,
            Up,
            UpLeft,
            Left,
            DownLeft,
            Down,
            DownRight
        }

        private static Dir8 QuantizeTo4(Vector2 v)
        {
            if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
                return v.x >= 0f ? Dir8.Right : Dir8.Left;

            return v.y >= 0f ? Dir8.Up : Dir8.Down;
        }

        private static Dir8 QuantizeTo8(Vector2 v)
        {
            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg; // -180..180
            if (angle < 0f) angle += 360f;                      // 0..360

            int oct = Mathf.FloorToInt((angle + 22.5f) / 45f) % 8;

            return oct switch
            {
                0 => Dir8.Right,
                1 => Dir8.UpRight,
                2 => Dir8.Up,
                3 => Dir8.UpLeft,
                4 => Dir8.Left,
                5 => Dir8.DownLeft,
                6 => Dir8.Down,
                7 => Dir8.DownRight,
                _ => Dir8.Right
            };
        }

        private bool TryGetExact(Dir8 d, out AnimationClipDef clip)
        {
            clip = d switch
            {
                Dir8.Right => right,
                Dir8.Left => left,
                Dir8.Up => up,
                Dir8.Down => down,
                Dir8.UpRight => upRight,
                Dir8.UpLeft => upLeft,
                Dir8.DownRight => downRight,
                Dir8.DownLeft => downLeft,
                _ => null
            };

            return clip != null;
        }

        private static (Dir8 dir, bool mirrorX, bool mirrorY) OppositeWithMirror(Dir8 d)
        {
            return d switch
            {
                Dir8.Right => (Dir8.Left, true, false),
                Dir8.Left => (Dir8.Right, true, false),
                Dir8.Up => (Dir8.Down, false, true),
                Dir8.Down => (Dir8.Up, false, true),

                Dir8.UpRight => (Dir8.DownLeft, true, true),
                Dir8.UpLeft => (Dir8.DownRight, true, true),
                Dir8.DownRight => (Dir8.UpLeft, true, true),
                Dir8.DownLeft => (Dir8.UpRight, true, true),

                _ => (d, false, false)
            };
        }

        private static (Dir8 dir, bool mirrorX, bool mirrorY) MirrorX(Dir8 d)
        {
            return d switch
            {
                Dir8.Right => (Dir8.Left, true, false),
                Dir8.Left => (Dir8.Right, true, false),

                Dir8.UpRight => (Dir8.UpLeft, true, false),
                Dir8.UpLeft => (Dir8.UpRight, true, false),

                Dir8.DownRight => (Dir8.DownLeft, true, false),
                Dir8.DownLeft => (Dir8.DownRight, true, false),

                _ => (d, false, false)
            };
        }

        private static (Dir8 dir, bool mirrorX, bool mirrorY) MirrorY(Dir8 d)
        {
            return d switch
            {
                Dir8.Up => (Dir8.Down, false, true),
                Dir8.Down => (Dir8.Up, false, true),

                Dir8.UpRight => (Dir8.DownRight, false, true),
                Dir8.DownRight => (Dir8.UpRight, false, true),

                Dir8.UpLeft => (Dir8.DownLeft, false, true),
                Dir8.DownLeft => (Dir8.UpLeft, false, true),

                _ => (d, false, false)
            };
        }

        private static (Dir8 dir, bool mirrorX, bool mirrorY) MirrorXY(Dir8 d)
        {
            var (opp, mx, my) = OppositeWithMirror(d);
            return (opp, mx, my);
        }

        private static Dir8 ToNearestCardinal(Dir8 d)
        {
            return d switch
            {
                Dir8.UpRight => Dir8.Up,
                Dir8.UpLeft => Dir8.Up,
                Dir8.DownRight => Dir8.Down,
                Dir8.DownLeft => Dir8.Down,
                _ => d
            };
        }
    }
}
