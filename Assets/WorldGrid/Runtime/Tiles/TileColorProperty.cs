using UnityEngine;

namespace WorldGrid.Runtime.Tiles
{
    /// <summary>
    /// Presentation semantic: tint color + optional brightness jitter + blend strength.
    /// Blend: 0 = no tint, 1 = full tint.
    /// </summary>
    public sealed class TileColorProperty : TileProperty
    {
        public readonly Color32 Color;
        public readonly float Jitter;
        public readonly float Blend;

        public TileColorProperty(Color32 color, float jitter = 0f, float blend = 1f)
        {
            Color = color;
            Jitter = Mathf.Clamp(jitter, 0f, 0.25f);
            Blend = Mathf.Clamp01(blend);
        }
    }
}
