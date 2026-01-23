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
        #region Inspector

        [SerializeField] private Button button;
        [SerializeField] private RawImage image;

        [Tooltip("Optional highlight graphic (Image, Outline, etc.).")]
        [SerializeField] private Graphic selectedHighlight;

        [Tooltip("Optional label (TextMeshPro).")]
        [SerializeField] private TMP_Text label;

        #endregion

        #region State

        public int TileId { get; private set; }

        #endregion

        #region Public API

        // Existing signature preserved
        public void Bind(
            int tileId,
            Texture atlas,
            RectUv uv,
            string labelText,
            Action<int> onClicked)
        {
            Bind(tileId, atlas, uv, labelText, onClicked, new Color32(255, 255, 255, 255));
        }

        // Existing overload preserved
        public void Bind(
            int tileId,
            Texture atlas,
            RectUv uv,
            string labelText,
            Action<int> onClicked,
            Color32 tint)
        {
            TileId = tileId;

            applyImage(atlas, uv, tint);
            applyLabel(labelText);
            applyClick(onClicked);
        }

        public void SetSelected(bool selected)
        {
            if (selectedHighlight != null)
                selectedHighlight.enabled = selected;
        }

        #endregion

        #region Apply

        private void applyImage(Texture atlas, RectUv uv, Color32 tint)
        {
            if (image == null)
                return;

            image.texture = atlas;
            image.uvRect = new Rect(
                uv.UMin,
                uv.VMin,
                uv.UMax - uv.UMin,
                uv.VMax - uv.VMin
            );

            image.color = tint;
        }

        private void applyLabel(string labelText)
        {
            if (label != null)
                label.text = labelText ?? string.Empty;
        }

        private void applyClick(Action<int> onClicked)
        {
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(TileId));
        }

        #endregion
    }
}
