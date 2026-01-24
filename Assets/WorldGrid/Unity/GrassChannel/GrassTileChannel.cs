using UnityEngine;

namespace WorldGrid.Runtime.Tiles
{
    /// <summary>
    /// Runtime compiled channel for grass semantics.
    /// Built by scanning tile IDs and reading GrassTileProperty via TileLibrary.TryGetProperty.
    /// </summary>
    public sealed class GrassTileChannel
    {
        public struct Info
        {
            public bool grassable;
            public float density;
            public Color32 tint;
            public float stiffness;
        }

        // Indexed by tileId. If tileId is unknown/unscanned => default Info (grassable=false).
        private readonly Info[] _byId;

        public int MaxTileIdScanned => _byId.Length - 1;

        public GrassTileChannel(Info[] byId)
        {
            _byId = byId ?? new Info[1];
        }

        public bool IsGrassable(int tileId)
        {
            if ((uint)tileId >= (uint)_byId.Length) return false;
            return _byId[tileId].grassable;
        }

        public Info Get(int tileId)
        {
            if ((uint)tileId >= (uint)_byId.Length) return default;
            return _byId[tileId];
        }

        /// <summary>
        /// Option A build: scan tile IDs [0..maxTileIdToScan] and pull GrassTileProperty via TileLibrary.
        /// </summary>
        public static GrassTileChannel BuildFrom(TileLibrary lib, int maxTileIdToScan)
        {
            if (lib == null) return new GrassTileChannel(new Info[1]);

            maxTileIdToScan = Mathf.Max(0, maxTileIdToScan);

            var arr = new Info[maxTileIdToScan + 1];

            for (int tileId = 0; tileId <= maxTileIdToScan; tileId++)
            {
                // default: no grass unless property exists
                arr[tileId] = default;

                if (lib.TryGetProperty<GrassTileProperty>(tileId, out var prop) && prop != null)
                {
                    arr[tileId] = new Info
                    {
                        grassable = prop.Grassable,
                        density = Mathf.Max(0f, prop.DensityMultiplier),
                        tint = prop.Tint,
                        stiffness = Mathf.Max(0.001f, prop.Stiffness),
                    };
                }
            }

            return new GrassTileChannel(arr);
        }
    }
}
