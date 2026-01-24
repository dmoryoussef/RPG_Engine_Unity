using System;
using UnityEngine;

namespace WorldGrid.Runtime.Tiles
{
    /// <summary>
    /// Authoring semantic: this tile can spawn grass + parameters.
    /// Compiled into GrassTileChannel for runtime use.
    /// </summary>
    [Serializable]
    public sealed class GrassTileProperty : TileProperty
    {
        [Tooltip("If false, this tile never spawns decorative grass.")]
        public bool Grassable = true;

        [Tooltip("Multiplier on global density (0 = none, 1 = normal, 2 = double, etc).")]
        [Range(0f, 4f)]
        public float DensityMultiplier = 1f;

        [Tooltip("Optional color influence for the grass on this tile (alpha ignored).")]
        public Color Tint = Color.white;

        [Tooltip("Optional: scales wind/push response for this tile's grass.")]
        [Range(0f, 2f)]
        public float Stiffness = 1f;
    }
}
