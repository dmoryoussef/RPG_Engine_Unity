using UnityEngine;
using RPG.Foundation;

namespace RPG.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class MailboxInteractable : InteractableBase
    {
        [Header("Message")]
        [TextArea][SerializeField] private string singleMessage = "You've got mail!";
        [SerializeField] private bool useSequence = false;
        [TextArea][SerializeField] private string[] messages;
        [SerializeField] private bool loopSequence = true;

        [Header("Runtime (read-only)")]
        [SerializeField] private int messageIndex = 0;

        protected override bool DoInteract()
        {
            string line = GetMessage();
            if (string.IsNullOrEmpty(line))
                return false; // nothing to say; treat as no-op

            Debug.Log(line); // replace with UI later
            return true;
        }

#if UNITY_EDITOR
        protected override void ValidateExtra()
        {
            if (useSequence && (messages == null || messages.Length == 0))
                Debug.Log($"<color=red>[Mailbox]</color> 'useSequence' is true but 'messages' is empty on '{name}'.");
            if (!useSequence && string.IsNullOrWhiteSpace(singleMessage))
                Debug.Log($"<color=red>[Mailbox]</color> 'singleMessage' is empty on '{name}'.");
        }
#endif

        private string GetMessage()
        {
            if (!useSequence)
                return singleMessage;

            if (messages == null || messages.Length == 0)
                return string.Empty;

            int idx = Mathf.Clamp(messageIndex, 0, messages.Length - 1);
            string line = messages[idx];

            if (messageIndex < messages.Length - 1)
                messageIndex++;
            else if (loopSequence && messages.Length > 0)
                messageIndex = 0;

            return line;
        }
    }
}
