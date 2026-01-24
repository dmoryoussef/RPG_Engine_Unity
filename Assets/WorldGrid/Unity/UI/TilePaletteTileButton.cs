using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldGrid.Runtime.Tiles;

namespace WorldGrid.Unity.UI
{
    [DisallowMultipleComponent]
    public sealed class TilePaletteTileButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private RawImage image;

        [Tooltip("Optional highlight graphic (Image, Outline, etc.).")]
        [SerializeField] private Graphic selectedHighlight;

        [Header("Labels")]
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text idLabel;

        public int TileId { get; private set; }

        private TileDef _def;
        private TilePaletteTooltipUI _tooltip;

        public void Bind(
            TileDef def,
            Texture atlas,
            RectUv uv,
            Action<int> onClicked,
            Color32 tint,
            TilePaletteTooltipUI tooltip = null)
        {
            if (def == null)
                return;

            _def = def;
            _tooltip = tooltip;

            TileId = def.TileId;

            applyImage(atlas, uv, tint);
            applyLabels(def);
            applyClick(onClicked);
        }

        public void SetSelected(bool selected)
        {
            if (selectedHighlight != null)
                selectedHighlight.enabled = selected;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_tooltip == null || _def == null)
                return;

            _tooltip.Show(_def);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _tooltip?.Hide();
        }

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

        private void applyLabels(TileDef def)
        {
            if (nameLabel != null)
            {
                bool hasName = !string.IsNullOrWhiteSpace(def.Name);
                nameLabel.gameObject.SetActive(hasName);
                if (hasName)
                    nameLabel.text = def.Name;
            }

            if (idLabel != null)
            {
                idLabel.gameObject.SetActive(true);
                idLabel.text = def.TileId.ToString();
            }
        }

        private void applyClick(Action<int> onClicked)
        {
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(TileId));
        }
    }
}
