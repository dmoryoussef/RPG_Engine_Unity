using UnityEngine;
using WorldPlacement.Runtime.Defs;
using WorldPlacement.Runtime.Grid;

namespace WorldPlacement.Unity.Assets
{
    [CreateAssetMenu(menuName = "WorldPlacement/Placement Definition", fileName = "PlacementDef")]
    public sealed class PlacementDefAsset : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "placement_id";
        [SerializeField] private string displayName = "Placeable";

        [Header("Visuals (Unity Only)")]
        [SerializeField] private GameObject prefab;
        [SerializeField] private Sprite icon; // optional, useful for UI

        [Header("Footprint (MVP authoring)")]
        [SerializeField] private int width = 1;
        [SerializeField] private int height = 1;
        [SerializeField] private Vector2Int pivot = Vector2Int.zero;
        [SerializeField] private FootprintCellKind[] cells = { FootprintCellKind.Solid };

        public string Id => id;
        public string DisplayName => displayName;

        public GameObject Prefab => prefab;
        public Sprite Icon => icon;

        public PlacementDef ToRuntime()
        {
            var fp = new PlacementFootprint(
                width,
                height,
                new Cell2i(pivot.x, pivot.y),
                (FootprintCellKind[])cells.Clone()
            );

            return new PlacementDef(id, displayName, fp);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
                id = name;

            if (width < 1) width = 1;
            if (height < 1) height = 1;

            pivot.x = Mathf.Clamp(pivot.x, 0, width - 1);
            pivot.y = Mathf.Clamp(pivot.y, 0, height - 1);

            int needed = width * height;
            if (cells == null || cells.Length != needed)
            {
                var newCells = new FootprintCellKind[needed];
                if (cells != null)
                {
                    int copy = Mathf.Min(cells.Length, newCells.Length);
                    for (int i = 0; i < copy; i++) newCells[i] = cells[i];
                }
                else
                {
                    newCells[0] = FootprintCellKind.Solid;
                }
                cells = newCells;
            }
        }
#endif
    }
}
