using System;
using UnityEngine;

namespace WorldGrid.Runtime.Tiles
{
    [Serializable]
    public sealed class MovementProperty : TileProperty
    {
        [SerializeField] private float _multiplier = 1;

        public float Multiplier => _multiplier;

        public override string ToString()
        {
            return $"Movement(mult={Multiplier})";
        }
    }
}
