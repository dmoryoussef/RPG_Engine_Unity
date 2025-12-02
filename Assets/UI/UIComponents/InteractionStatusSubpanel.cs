using Interaction;
using Inspection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI
{
    /// <summary>
    /// Inspection subpanel that shows who is targeting the inspected object
    /// and whether that entity is currently able to interact with it
    /// (range + facing + canInteract + reason).
    ///
    /// Behavior:
    /// - OnPopulate: reads initial InteractionInfo from context just to discover
    ///   the interactor and interactable roots.
    /// - Each frame while open: calls interactor.BuildGateInfo(interactable)
    ///   to get fresh gating info and updates the labels.
    /// </summary>
    public sealed class InteractionStatusSubpanel : MonoBehaviour, IInspectionSubpanel
    {
        [Header("Root")]
        [SerializeField]
        private GameObject _rootContainer;

        [Header("Labels")]
        [SerializeField]
        private TMP_Text _targetingEntityLabel;

        [SerializeField]
        private TMP_Text _distanceLabel;

        [SerializeField]
        private TMP_Text _facingLabel;

        [SerializeField]
        private TMP_Text _canInteractLabel;

        [SerializeField]
        private TMP_Text _reasonLabel;

        [Header("Visuals")]
        [SerializeField]
        private Color _canInteractColor = Color.green;

        [SerializeField]
        private Color _cannotInteractColor = Color.red;

        private bool _isOpen;

        // Cached wiring discovered via context.InteractionInfo
        private PlayerInteractor _interactor;
        private InteractableBase _interactable;

        private void Awake()
        {
            if (!_rootContainer)
                _rootContainer = gameObject;

            SetVisible(false);
            ClearLabels();
        }

        // ============================================================
        // IInspectionSubpanel
        // ============================================================

        public void OnPopulate(InspectionData data, InspectionPanelContext context)
        {
            // Reset caches
            _interactor = null;
            _interactable = null;

            if (context == null)
            {
                ClearLabels();
                SetVisible(false);
                return;
            }

            var info = context.InteractionInfo;

            // No interaction info? Nothing to show.
            if (!info.HasInteractor || !info.HasInteractable)
            {
                ClearLabels();
                SetVisible(false);
                return;
            }

            // Try to cache the live components from the roots.
            if (info.InteractorRoot)
                _interactor = info.InteractorRoot.GetComponentInParent<PlayerInteractor>();

            if (info.InteractableRoot)
                _interactable = info.InteractableRoot.GetComponentInParent<InteractableBase>();

            // If we can't find both, hide.
            if (_interactor == null || _interactable == null)
            {
                ClearLabels();
                SetVisible(false);
                return;
            }

            // If we are already open, immediately show a first update.
            if (_isOpen)
            {
                SetVisible(true);
                RefreshFromInteractor();
            }
        }

        public void OnOpen()
        {
            _isOpen = true;

            if (_interactor != null && _interactable != null)
            {
                SetVisible(true);
                RefreshFromInteractor();
            }
            else
            {
                SetVisible(false);
            }
        }

        public void OnClose()
        {
            _isOpen = false;
            SetVisible(false);
        }

        // ============================================================
        // Per-frame live updates
        // ============================================================

        private void Update()
        {
            if (!_isOpen)
                return;

            if (_interactor == null || _interactable == null)
            {
                SetVisible(false);
                ClearLabels();
                return;
            }

            RefreshFromInteractor();
        }

        private void RefreshFromInteractor()
        {
            // Ask the interactor for fresh gating data.
            InteractionGateInfo info = _interactor.BuildGateInfo(_interactable);

            // If either root is missing, hide.
            if (!info.HasInteractor || !info.HasInteractable)
            {
                SetVisible(false);
                ClearLabels();
                return;
            }

            // Ensure we're visible while we have valid data.
            SetVisible(true);
            PopulateLabels(info);
        }

        // ============================================================
        // Helpers
        // ============================================================

        private void SetVisible(bool visible)
        {
            if (_rootContainer)
                _rootContainer.SetActive(visible);
        }

        private void ClearLabels()
        {
            if (_targetingEntityLabel) _targetingEntityLabel.text = string.Empty;
            if (_distanceLabel) _distanceLabel.text = string.Empty;
            if (_facingLabel) _facingLabel.text = string.Empty;
            if (_canInteractLabel) _canInteractLabel.text = string.Empty;
            if (_reasonLabel) _reasonLabel.text = string.Empty;
        }

        private void PopulateLabels(InteractionGateInfo info)
        {
            // Targeting entity
            if (_targetingEntityLabel)
            {
                string name = info.InteractorRoot ? info.InteractorRoot.name : "Unknown";
                _targetingEntityLabel.text = $"Targeting: {name}";
            }

            // Distance
            if (_distanceLabel)
            {
                string rangeText = info.MaxDistance > 0f
                    ? $"{info.Distance:0.00} / {info.MaxDistance:0.00}"
                    : $"{info.Distance:0.00}";

                string stateText = info.InRange ? "in range" : "out of range";
                _distanceLabel.text = $"Distance: {rangeText} ({stateText})";
            }

            // Facing
            if (_facingLabel)
            {
                string facingState = info.FacingOk ? "facing" : "not facing";
                _facingLabel.text =
                    $"Facing: {facingState} (dot {info.FacingDot:0.00} / {info.FacingThreshold:0.00})";
            }

            // CanInteract
            if (_canInteractLabel)
            {
                _canInteractLabel.text = info.CanInteract ? "Can Interact" : "Cannot Interact";
                _canInteractLabel.color = info.CanInteract ? _canInteractColor : _cannotInteractColor;
            }

            // Reason
            if (_reasonLabel)
            {
                _reasonLabel.text = BuildReasonLine(info);
            }
        }

        private static string BuildReasonLine(InteractionGateInfo info)
        {
            if (info.CanInteract)
                return "Ready to interact.";

            if (!info.LastFailReason.HasValue)
            {
                if (!info.InRange && !info.FacingOk)
                    return "Out of range and not facing the target.";
                if (!info.InRange)
                    return "Out of range.";
                if (!info.FacingOk)
                    return "Not facing the target.";
                return "Interaction blocked.";
            }

            switch (info.LastFailReason.Value)
            {
                case InteractionFailReason.Cooldown:
                    return "On cooldown.";
                case InteractionFailReason.OutOfUses:
                    return "No uses remaining.";
                case InteractionFailReason.Locked:
                    return "Locked.";
                case InteractionFailReason.AlreadyInDesiredState:
                    return "Already in the desired state.";
                case InteractionFailReason.Other:
                default:
                    return "Interaction failed.";
            }
        }
    }
}
