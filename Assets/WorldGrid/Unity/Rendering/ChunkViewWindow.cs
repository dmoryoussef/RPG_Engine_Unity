using System;
using UnityEngine;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.Rendering;

namespace WorldGrid.Unity.Rendering
{
    /// <summary>
    /// Unity-facing adapter that stores chunk visibility window and notifies listeners when it changes.
    /// Single source of truth for visible chunk window.
    /// </summary>
    public sealed class ChunkViewWindow : MonoBehaviour, IChunkViewWindowProvider
    {
        [SerializeField] private Vector2Int _viewSize = new Vector2Int(4, 4);
        [SerializeField] private Vector2Int _viewMin = Vector2Int.zero;

        public ChunkCoord ViewMin => new ChunkCoord(_viewMin.x, _viewMin.y);
        public Vector2Int ViewSize => _viewSize;

        public event Action<ChunkCoord, Vector2Int> ViewChanged;

        public void SetView(ChunkCoord min, Vector2Int size)
        {
            size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));

            var minV = new Vector2Int(min.X, min.Y);
            if (minV == _viewMin && size == _viewSize)
            {
                return;
            }

            _viewMin = minV;
            _viewSize = size;
            ViewChanged?.Invoke(min, size);
        }

        private void OnValidate()
        {
            _viewSize = new Vector2Int(Mathf.Max(1, _viewSize.x), Mathf.Max(1, _viewSize.y));
        }
    }
}
