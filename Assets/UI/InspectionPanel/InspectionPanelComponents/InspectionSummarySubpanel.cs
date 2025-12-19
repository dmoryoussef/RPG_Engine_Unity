using UnityEngine;
using TMPro;
using Inspection;

namespace UI
{
    /// <summary>
    /// Inspection Facts Subpanel
    /// -------------------------
    /// Purpose:
    /// - Render descriptive "facts" about the inspected object using data produced
    ///   by the Inspection system (InspectionData).
    ///
    /// Data Source:
    /// - Directly reads InspectionData fields:
    ///   - DisplayName
    ///   - ShortDescription
    ///   - LongDescription
    ///   - (optionally) other inspection-side factual fields you add later.
    ///
    /// Responsibilities:
    /// - Bind title/summary/body text to the core inspection data.
    /// - Present stable, inspection-oriented information (not interaction state).
    ///
    /// This subpanel does NOT know about interaction buttons or states; it is
    /// strictly about describing the object from the inspection system's perspective.
    /// </summary>
    public sealed class InspectionSummarySubpanel : MonoBehaviour, IInspectionSubpanel
    {
        [Header("Labels (Inspection Data)")]
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _shortLabel;
        [SerializeField] private TMP_Text _longLabel;

        private InspectionData _lastDataRef;

        public void OnPopulate(InspectionData data, InspectionPanelContext context)
        {
            if (data == null)
            {
                _lastDataRef = null;
                if (_titleLabel != null) _titleLabel.text = string.Empty;
                if (_shortLabel != null) _shortLabel.text = string.Empty;
                if (_longLabel != null) _longLabel.text = string.Empty;
                return;
            }

            bool isNewTarget = !ReferenceEquals(_lastDataRef, data);
            _lastDataRef = data;

            if (_titleLabel != null)
                _titleLabel.text = data.DisplayName ?? string.Empty;

            if (_shortLabel != null)
                _shortLabel.text = data.ShortDescription ?? string.Empty;

            if (_longLabel != null)
            {
                // Only clear on new target; otherwise keep whatever was already shown
                if (isNewTarget)
                    _longLabel.text = string.Empty;

                if (!string.IsNullOrEmpty(data.LongDescription))
                    _longLabel.text = data.LongDescription;
            }
        }

        public void OnOpen() { if (!gameObject.activeSelf) gameObject.SetActive(true); }
        public void OnClose() { if (gameObject.activeSelf) gameObject.SetActive(false); }
    }

}
