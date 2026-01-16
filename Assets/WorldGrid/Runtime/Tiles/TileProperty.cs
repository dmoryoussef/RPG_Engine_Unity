using System;

namespace WorldGrid.Runtime.Tiles
{
    /// <summary>
    /// Base class for optional, extensible tile semantics.
    /// These are intended for AUTHORING / BUILD time and then compiled into fast runtime channels.
    /// </summary>
    [Serializable]
    public abstract class TileProperty
    {
        // Marker base type. Add common validation helpers here later if needed.
    }
}
