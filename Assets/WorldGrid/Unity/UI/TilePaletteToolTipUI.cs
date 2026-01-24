using System.Text;
using TMPro;
using UnityEngine;
using WorldGrid.Runtime.Tiles;

namespace WorldGrid.Unity.UI
{
    /// <summary>
    /// Fixed-position tile inspection tooltip panel.
    /// Lives in a single place in the UI; only content/visibility changes.
    /// Event-driven (show/hide called by tile buttons). No Update loop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TilePaletteTooltipUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;

        [Header("Content Options")]
        [SerializeField] private bool showUv = false;

        private readonly StringBuilder _sb = new StringBuilder(256);

        private void Awake()
        {
            // Safe defaults for a tooltip panel: never block input.
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            HideImmediate();
        }

        public void Show(TileDef def)
        {
            if (def == null || canvasGroup == null)
                return;

            // Title: Name if present, else ID
            if (titleText != null)
            {
                string title = !string.IsNullOrWhiteSpace(def.Name) ? def.Name : def.TileId.ToString();
                titleText.text = title;
            }

            // Body: Id, Name, Tags, Props, optional UV
            if (bodyText != null)
            {
                _sb.Clear();

                _sb.Append("Id: ").Append(def.TileId);

                if (!string.IsNullOrWhiteSpace(def.Name))
                    _sb.Append("\nName: ").Append(def.Name);

                if (def.Tags != null && def.Tags.Count > 0)
                {
                    _sb.Append("\nTags: ");
                    for (int i = 0; i < def.Tags.Count; i++)
                    {
                        if (i > 0) _sb.Append(", ");
                        _sb.Append(def.Tags[i]);
                    }
                }

                if (def.Properties != null && def.Properties.Count > 0)
                {
                    _sb.Append("\nProps: ");
                    for (int i = 0; i < def.Properties.Count; i++)
                    {
                        if (i > 0) _sb.Append(", ");
                        var p = def.Properties[i];
                        _sb.Append(p != null ? p.GetType().Name : "null");
                    }
                }

                if (showUv)
                {
                    var uv = def.Uv;
                    _sb.Append("\nUV: (")
                        .Append(uv.UMin.ToString("F2")).Append(", ")
                        .Append(uv.VMin.ToString("F2")).Append(") → (")
                        .Append(uv.UMax.ToString("F2")).Append(", ")
                        .Append(uv.VMax.ToString("F2")).Append(")");
                }

                bodyText.text = _sb.ToString();
            }

            canvasGroup.alpha = 1f;
        }

        public void Hide()
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = 0f;
        }

        public void HideImmediate()
        {
            Hide();
        }
    }
}
