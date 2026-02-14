// IChunkViewWindowProvider.cs
using System;
using UnityEngine;
using WorldGrid.Runtime.Coords;

namespace WorldGrid.Runtime.Rendering
{
    public interface IChunkViewWindowProvider
    {
        ChunkCoord ViewMin { get; }
        Vector2Int ViewSize { get; }
        event Action<ChunkCoord, Vector2Int> ViewChanged;
    }
}
