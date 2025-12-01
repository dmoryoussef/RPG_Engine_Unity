using UnityEngine;
using Interaction;



    [DisallowMultipleComponent] 
    [RequireComponent(typeof(Collider2D))]
    public class MailboxInteractable : InteractableBase
    {
        [Header("Message")]
        [TextArea][SerializeField] private string _singleMessage = "You've got mail!";
        [SerializeField] private bool _useSequence = false;
        [TextArea][SerializeField] private string[] _messages;
        [SerializeField] private bool _loopSequence = true;

        [Header("Runtime (read-only)")]
        [SerializeField] private int _messageIndex = 0;

        protected override bool DoInteract()
        {
            string line = GetMessage();
            if (string.IsNullOrEmpty(line))
                return false; // nothing to say; treat as no-op

            Debug.Log(line); // replace with UI later
            return true;
        }

#if UNITY_EDITOR
        protected void ValidateExtra()
        {
            if (_useSequence && (_messages == null || _messages.Length == 0))
                Debug.Log($"<color=red>[Mailbox]</color> 'useSequence' is true but 'messages' is empty on '{name}'.");
            if (!_useSequence && string.IsNullOrWhiteSpace(_singleMessage))
                Debug.Log($"<color=red>[Mailbox]</color> 'singleMessage' is empty on '{name}'.");
        }
#endif

        private string GetMessage()
        {
            if (!_useSequence)
                return _singleMessage;

            if (_messages == null || _messages.Length == 0)
                return string.Empty;

            int idx = Mathf.Clamp(_messageIndex, 0, _messages.Length - 1);
            string line = _messages[idx];

            if (_messageIndex < _messages.Length - 1)
                _messageIndex++;
            else if (_loopSequence && _messages.Length > 0)
                _messageIndex = 0;

            return line;
        }
    }

