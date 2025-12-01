using UnityEngine;
using TMPro;
using Inspection;

namespace UI
{
    public sealed class InspectionStateSummarySubpanel : MonoBehaviour, IInspectionSubpanel
    {
        [Header("Display")]
        [SerializeField] private TMP_Text _stateLabel;

        [Tooltip("Optional prefix, e.g. 'Status:'. Leave empty for none.")]
        [SerializeField] private string _prefix = "";

        [Tooltip("Separator between multiple state lines.")]
        [SerializeField] private string _separator = "\n";

        public void OnPopulate(InspectionData data, InspectionPanelContext context)
        {
            if (_stateLabel == null)
            {
                Debug.LogWarning($"{name}: State label not assigned.", this);
                return;
            }

            if (context == null || context.States.Count == 0)
            {
                _stateLabel.text = string.Empty;
                return;
            }

            var sb = new System.Text.StringBuilder();

            if (!string.IsNullOrEmpty(_prefix))
            {
                sb.Append(_prefix);
                sb.Append(" ");
            }

            for (int i = 0; i < context.States.Count; i++)
            {
                if (i > 0)
                    sb.Append(_separator);

                sb.Append(context.States[i].Label);
            }

            _stateLabel.text = sb.ToString();
        }

        public void OnOpen()
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
        }

        public void OnClose()
        {
            if (_stateLabel != null)
                _stateLabel.text = string.Empty;

            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }
}
