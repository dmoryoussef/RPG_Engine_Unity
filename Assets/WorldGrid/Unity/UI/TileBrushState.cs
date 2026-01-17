using UnityEngine;
using System;

namespace WorldGrid.Unity.UI
{
    [CreateAssetMenu(
        menuName = "WorldGrid/UI/Tile Brush State",
        fileName = "TileBrushState")]
    public sealed class TileBrushState : ScriptableObject
    {
        [SerializeField]
        private int _selectedTileId = -1;

        public int selectedTileId
        {
            get => _selectedTileId;
            set
            {
                if (_selectedTileId == value)
                    return;

                _selectedTileId = value;
                OnSelectionChanged?.Invoke(_selectedTileId);
            }
        }

        public int eraseTileId = 0;
        public int brushRadius = 0;

        /// <summary>
        /// Fired whenever selectedTileId changes (UI, ESC, tools, etc.)
        /// </summary>
        public event Action<int> OnSelectionChanged;
    }
}
