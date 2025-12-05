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
    /// - OnPopulate: reads InteractionInfo from the context to discover the
    ///   interactor and interactable roots. If none, it hides its own UI but
    ///   does NOT affect the overall inspection panel root.
    /// - OnOpen: if we have valid wiring, shows the panel and forces an immediate
    ///   refresh; otherwise hides itself.
    /// - Update: while open and wired, polls the PlayerInteractor for a fresh
    ///   InteractionGateInfo each frame and updates the labels.
    ///
    /// This subpanel MUST NOT deactivate the InspectionPanelRoot. It only
    /// toggles its own _rootContainer.
    /// </summary>
    public sealed class InteractionStatusSubpanel : MonoBehaviour, IInspectionSubpanel
    {
        [Header("Root")]
        [SerializeField]
        private GameObject _rootContainer;

        [Header("Labels")]
        [SerializeField] private TMP_Text _targetingEntityLabel;
        [SerializeField] private TMP_Text _distanceLabel;
        [SerializeField] private TMP_Text _facingLabel;
        [SerializeField] private TMP_Text _canInteractLabel;
        [SerializeField] private TMP_Text _reasonLabel;

        [Header("Icon")]
        [SerializeField] private Image _canInteractIcon;
        [SerializeField] private Sprite _canInteractSprite;
        [SerializeField] private Sprite _cannotInteractSprite;

        [Header("Visuals")]
        [SerializeField] private Color _canInteractColor = Color.green;
        [SerializeField] private Color _cannotInteractColor = Color.red;

        // Runtime state
        private bool _isOpen;

        // Cached wiring discovered via context.InteractionInfo
        private PlayerInteractor _interactor;
        private InteractableBase _interactable;

        private void Awake()
        {
            // Default to our own GameObject if no explicit container is assigned,
            // but we still guard against accidentally turning off the panel root.
            if (_rootContainer == null)
                _rootContainer = gameObject;

            SetVisible(false);
            ClearLabels();
        }

        // ============================================================
        // IInspectionSubpanel
        // ============================================================

        public void OnPopulate(InspectionData data, InspectionPanelContext context)
        {
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

            // Cache interactor / interactable roots from the snapshot.
            // (These are GameObjects; we want the components.)
            if (info.InteractorRoot != null)
                _interactor = info.InteractorRoot.GetComponentInParent<PlayerInteractor>();

            if (info.InteractableRoot != null)
                _interactable = info.InteractableRoot.GetComponentInParent<InteractableBase>();

            // If we are already open, immediately show a first update.
            if (_isOpen && _interactor != null && _interactable != null)
            {
                SetVisible(true);
                RefreshFromInteractor();
            }
            else
            {
                // We have interaction info in the snapshot but couldn't resolve components;
                // hide just this subpanel.
                SetVisible(false);
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
                return;
            }

            RefreshFromInteractor();
        }

        private void RefreshFromInteractor()
        {
            if (_interactor == null || _interactable == null)
            {
                SetVisible(false);
                return;
            }

            // FIXED: call the method on PlayerInteractor instead of treating it like a delegate
            var info = _interactor.BuildGateInfo(_interactable);

            // If we somehow lost interaction context, just hide our own UI.
            if (!info.HasInteractor || !info.HasInteractable)
            {
                ClearLabels();
                SetVisible(false);
                return;
            }

            SetVisible(true);
            PopulateLabels(info);
        }

        // ============================================================
        // Helpers
        // ============================================================

        private void SetVisible(bool visible)
        {
            if (_rootContainer == null)
                return;

            // Safety: never let this subpanel turn off the entire inspection panel root.
            var panelRoot = _rootContainer.GetComponent<InspectionPanelRoot>() ??
                            _rootContainer.GetComponentInParent<InspectionPanelRoot>();

            if (!visible && panelRoot != null && _rootContainer == panelRoot.gameObject)
            {
                Debug.LogWarning(
                    $"{nameof(InteractionStatusSubpanel)} on '{name}' " +
                    "was asked to hide its Root Container, but that container is the " +
                    "InspectionPanelRoot. Assign a child GameObject as the Root Container instead.",
                    this);
                return;
            }

            _rootContainer.SetActive(visible);
        }

        private void ClearLabels()
        {
            if (_targetingEntityLabel) _targetingEntityLabel.text = string.Empty;
            if (_distanceLabel) _distanceLabel.text = string.Empty;
            if (_facingLabel) _facingLabel.text = string.Empty;
            if (_canInteractLabel) _canInteractLabel.text = string.Empty;
            if (_reasonLabel) _reasonLabel.text = string.Empty;

            if (_canInteractIcon)
            {
                _canInteractIcon.enabled = false;
                _canInteractIcon.sprite = null;
            }
        }

        private void PopulateLabels(InteractionGateInfo info)
        {
            // Targeting entity
            if (_targetingEntityLabel)
            {
                string name = info.InteractorRoot ? info.InteractorRoot.name : "Unknown";
                _targetingEntityLabel.text = name;
            }

            // Distance
            if (_distanceLabel)
            {
                _distanceLabel.text = info.InRange
                    ? $"{info.Distance:0.0}m"
                    : $"{info.Distance:0.0}m (out of range)";
            }

            // Facing
            if (_facingLabel)
            {
                _facingLabel.text = info.FacingOk
                    ? $"{info.FacingDot:0.00}"
                    : $"{info.FacingDot:0.00} (bad facing)";
            }

            bool canInteract = info.CanInteract;

            // Can interact text + color
            if (_canInteractLabel)
            {
                _canInteractLabel.text = canInteract ? "Can interact" : "Cannot interact";
                _canInteractLabel.color = canInteract ? _canInteractColor : _cannotInteractColor;
            }

            // Reason (nullable)
            if (_reasonLabel)
            {
                if (!canInteract && info.LastFailReason.HasValue)
                    _reasonLabel.text = GetReasonText(info.LastFailReason.Value);
                else
                    _reasonLabel.text = string.Empty;
            }

            // Icon
            if (_canInteractIcon)
            {
                _canInteractIcon.enabled = true;
                _canInteractIcon.sprite = canInteract ? _canInteractSprite : _cannotInteractSprite;
                _canInteractIcon.color = canInteract ? _canInteractColor : _cannotInteractColor;
            }
        }

        private string GetReasonText(InteractionFailReason reason)
        {
            switch (reason)
            {
                case InteractionFailReason.None:
                    return string.Empty;
                case InteractionFailReason.OutOfRange:
                    return "Too far away.";
                case InteractionFailReason.NotFacing:
                    return "Not facing target.";
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
