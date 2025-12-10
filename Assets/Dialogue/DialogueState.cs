using UnityEngine;
using State;
using Logging;
using Dialogue;

namespace State
{
    [DisallowMultipleComponent]
    public class DialogueState : BaseState
    {
        [Header("Dialogue")]
        [SerializeField] private DialogueGraphAsset _dialogueAsset;

        [SerializeField] private DialoguePanelView _dialoguePanel;

        [Header("Description")]
        [SerializeField] private string _descriptionCategory = "NPC";
        [SerializeField] private int _descriptionPriority = 10;

        private const string SystemTag = "DialogueState";

        public override StateResult TryStateChange(StateChangeContext context)
        {
            if (_dialogueAsset == null)
            {
                var generic = StateResult.Fail("no_dialogue_asset");
                GameLog.LogWarning(
                    this,
                    system: SystemTag,
                    action: "TryStateChange",
                    message: "No DialogueGraphAsset assigned.");
                return Report(generic);
            }

            if (_dialoguePanel == null)
            {
                var generic = StateResult.Fail("no_dialogue_panel");
                GameLog.LogError(
                    this,
                    system: SystemTag,
                    action: "TryStateChange",
                    message: "DialoguePanelView reference not assigned on DialogueState.");
                return Report(generic);
            }

            var block = CheckBlockers();
            if (!block.IsSuccess)
            {
                var generic = StateResult.Blocked(block.Message ?? "blocked");
                return Report(generic);
            }

            _dialoguePanel.StartConversation(_dialogueAsset);

            var result = StateResult.Succeed("dialogue_started");
            return Report(result);
        }

        public override string GetDescriptionText() => "Talk";
        public override int GetDescriptionPriority() => _descriptionPriority;
        public override string GetDescriptionCategory() => _descriptionCategory;
    }
}
