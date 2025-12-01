using UnityEngine;

namespace Interaction
{
    /// <summary>
    /// Ray-based interactable for open/close. Lock is handled internally by OpenCloseState.
    /// Demonstrates how to map state-change results to InteractionFailReason.
    /// </summary>
    public class OpenCloseInteractable : InteractableBase
    {
        [Header("References")]
        [SerializeField] private OpenCloseState openCloseState;

        private void Reset()
        {
            openCloseState = GetComponent<OpenCloseState>() ?? GetComponentInParent<OpenCloseState>(true);
        }

        protected override void ValidateExtra(bool isEditorPhase)
        {
            if (!openCloseState)
                openCloseState = GetComponent<OpenCloseState>() ?? GetComponentInParent<OpenCloseState>(true);

            if (!openCloseState && !isEditorPhase)
            {
                Debug.Log($"<color=red>[OpenCloseInteractable]</color> Missing OpenCloseState on '{name}'.");
            }
        }

        protected override bool DoInteract()
        {
            if (!openCloseState)
            {
                OnInteractionFailed(InteractionFailReason.Other);
                return false;
            }

            var result = openCloseState.TryStateChange(OpenCloseAction.Toggle);

            switch (result)
            {
                case StateChangeResult.Opened:
                    Debug.Log("Opened.");
                    return true;

                case StateChangeResult.Closed:
                    Debug.Log("Closed.");
                    return true;

                case StateChangeResult.AlreadyOpen:
                case StateChangeResult.AlreadyClosed:
                    Debug.Log("No change (already in desired state).");
                    OnInteractionFailed(InteractionFailReason.AlreadyInDesiredState);
                    return false;

                case StateChangeResult.FailedLocked:
                    Debug.Log("It's locked.");
                    OnInteractionFailed(InteractionFailReason.Locked);
                    return false;

                default:
                    Debug.Log("Nothing happened.");
                    OnInteractionFailed(InteractionFailReason.Other);
                    return false;
            }
        }
    }
}
