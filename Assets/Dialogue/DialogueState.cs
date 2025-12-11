using UnityEngine;
using Logging;
using Dialogue;

namespace State
{
    public enum DialogueAction
    {
        Toggle,
        Start,
        Stop
    }

    public enum DialogueResult
    {
        Started,
        Stopped,
        AlreadyTalking,
        AlreadyIdle,
        Failed,
        FailedBlocked
    }

    [DisallowMultipleComponent]
    public class DialogueState : BaseState
    {
        [Header("State")]
        [SerializeField]
        private bool _isTalking = false;
        public bool IsTalking => _isTalking;

        [Header("Dialogue")]
        [SerializeField] private DialogueGraphAsset _dialogueAsset;

        [Tooltip("Prefab for the dialogue panel UI.")]
        [SerializeField] private DialoguePanelView _dialoguePanelPrefab;

        [Tooltip("Optional explicit canvas root. If null, first Canvas in the scene will be used.")]
        [SerializeField] private Transform _canvasOverride;

        private DialoguePanelView _currentPanel;
        private Transform _canvasRuntime;

        [Header("Description")]
        [SerializeField] private string _descriptionCategory = "NPC";
        [SerializeField] private int _descriptionPriority = 10;

        private const string SystemTag = "DialogueState";

        private void Awake()
        {
            EnsureCanvas();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureCanvas();
        }
#endif

        // --------- Canvas / Panel helpers ---------

        private void EnsureCanvas()
        {
            if (_canvasRuntime != null)
                return;

            if (_canvasOverride != null)
            {
                _canvasRuntime = _canvasOverride;
                return;
            }

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                _canvasRuntime = canvas.transform;
            }
        }

        private bool EnsurePanel()
        {
            if (_currentPanel != null)
                return true;

            if (_dialoguePanelPrefab == null)
            {
                GameLog.LogError(
                    this,
                    system: SystemTag,
                    action: "EnsurePanel",
                    message: "DialoguePanelView prefab not assigned on DialogueState.");
                return false;
            }

            EnsureCanvas();

            if (_canvasRuntime != null)
                _currentPanel = Object.Instantiate(_dialoguePanelPrefab, _canvasRuntime);
            else
                _currentPanel = Object.Instantiate(_dialoguePanelPrefab); // fall back to root

            return _currentPanel != null;
        }

        // --------- Internal stop helper ---------

        private void StopDialogue(in StateChangeContext context)
        {
            if (!_isTalking)
                return;

            if (_currentPanel != null)
            {
                // Assumes DialoguePanelView.Cancel() will end any active session and hide the panel.
                _currentPanel.Cancel();
            }

            _isTalking = false;
            NotifyStateChanged();
        }

        // --------- Domain-level API (like OpenCloseState) ---------

        /// <summary>
        /// Domain-level toggle / start / stop for dialogue.
        /// Handles blockers, panel spawning, and state flag flips.
        /// </summary>
        public DialogueResult TryStateChange(DialogueAction action, in StateChangeContext context)
        {
            bool desiredTalking = action switch
            {
                DialogueAction.Toggle => !_isTalking,
                DialogueAction.Start => true,
                DialogueAction.Stop => false,
                _ => _isTalking
            };

            // No change requested.
            if (desiredTalking == _isTalking)
                return _isTalking ? DialogueResult.AlreadyTalking : DialogueResult.AlreadyIdle;

            // Going Idle → Talking
            if (desiredTalking)
            {
                if (_dialogueAsset == null)
                {
                    GameLog.LogWarning(
                        this,
                        system: SystemTag,
                        action: "TryStateChange",
                        message: "No DialogueGraphAsset assigned.");
                    return DialogueResult.Failed;
                }

                // Check blockers before starting a conversation (same pattern as OpenCloseState).
                var block = CheckBlockers();
                if (!block.IsSuccess)
                {
                    return DialogueResult.FailedBlocked;
                }

                if (!EnsurePanel())
                    return DialogueResult.Failed;

                _currentPanel.StartConversation(_dialogueAsset);
            }
            // Going Talking → Idle
            else
            {
                StopDialogue(context);
            }

            _isTalking = desiredTalking;
            NotifyStateChanged();

            return _isTalking ? DialogueResult.Started : DialogueResult.Stopped;
        }

        // --------- Interaction-facing API (used by InteractableComponent) ---------

        public override StateResult TryStateChange(StateChangeContext context)
        {
            // Interpret channel to decide how to react.
            // "Interact"   → toggle idle ↔ talking
            // "Cancel"     → force stop (Escape)
            // other       → default to toggle (for now)

            DialogueResult domainResult;

            switch (context.Channel)
            {
                case "Cancel":
                    domainResult = TryStateChange(DialogueAction.Stop, context);
                    break;

                case "Interact":
                default:
                    domainResult = TryStateChange(DialogueAction.Toggle, context);
                    break;
            }

            var generic = domainResult switch
            {
                DialogueResult.Started =>
                    StateResult.Succeed("talking"),

                DialogueResult.Stopped =>
                    StateResult.Succeed("idle"),

                DialogueResult.AlreadyTalking or DialogueResult.AlreadyIdle =>
                    StateResult.AlreadyInState("already_in_state"),

                DialogueResult.FailedBlocked =>
                    StateResult.Blocked("blocked"),

                _ =>
                    StateResult.Fail("failed")
            };

            return Report(generic);
        }

        // --------- Pre-change hooks: range + focus auto-stop ---------

        public override void OnPreStateChangePotentialExited(StateChangeContext context)
        {
            base.OnPreStateChangePotentialExited(context);

            // Leaving interaction range while talking → auto-stop.
            if (_isTalking && context.Channel == "RangeExit")
            {
                TryStateChange(DialogueAction.Stop, context);
            }
        }

        public override void OnPreStateChangeImminentExited(StateChangeContext context)
        {
            base.OnPreStateChangeImminentExited(context);

            // Losing focus while talking → auto-stop.
            if (_isTalking && context.Channel == "FocusLost")
            {
                TryStateChange(DialogueAction.Stop, context);
            }
        }

        // --------- Description (for panels/tooling) ---------

        public override string GetDescriptionText()
            => IsTalking ? "Talking" : "Idle";

        public override int GetDescriptionPriority()
            => _descriptionPriority;

        public override string GetDescriptionCategory()
            => _descriptionCategory;
    }
}
