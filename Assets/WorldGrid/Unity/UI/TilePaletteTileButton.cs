using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldGrid.Runtime.Tiles;

namespace WorldGrid.Unity.UI
{
    [DisallowMultipleComponent]
    public sealed class TilePaletteTileButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private RawImage image;

        [Tooltip("Optional highlight graphic (Image, Outline, etc.)")]
        [SerializeField] private Graphic selectedHighlight;

        [Tooltip("Optional label (Text or TMP adapter)")]
        [SerializeField] private TMP_Text label;

        public int TileId { get; private set; }

        public void Bind(
            int tileId,
            Texture atlas,
            RectUv uv,
            string label,
            Action<int> onClicked)
        {
            TileId = tileId;

            if (image != null)
            {
                image.texture = atlas;
                image.uvRect = new Rect(
                    uv.UMin,
                    uv.VMin,
                    uv.UMax - uv.UMin,
                    uv.VMax - uv.VMin
                );
            }

            if (this.label != null)
                this.label.text = label ?? string.Empty;

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClicked?.Invoke(TileId));
            }
        }

        public void SetSelected(bool selected)
        {
            if (selectedHighlight != null)
                selectedHighlight.enabled = selected;
        }
    }
}
