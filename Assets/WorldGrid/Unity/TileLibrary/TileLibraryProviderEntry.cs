using System;
using UnityEngine;
using WorldGrid.Unity.Assets;

namespace WorldGrid.Runtime.Tiles
{
    [Serializable]
    public sealed class TileLibraryProviderEntry
    {
        [Tooltip("Consumer-facing key (e.g. \"world\", \"debug\", \"interior\").")]
        public TileLibraryKey key;

        [Tooltip("Authoring-time tile definitions + atlas source settings.")]
        public TileLibraryAsset asset;

        [Tooltip("Optional: override material template used to instance the atlas material for this library.")]
        public Material materialTemplateOverride;

        public bool IsValid => !key.IsEmpty && asset != null;
    }
}
