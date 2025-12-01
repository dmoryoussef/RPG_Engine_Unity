using UnityEngine;
using Interaction;

/// <summary>
/// Interactable that changes a LockState using the unified API.
/// Physics-agnostic: selection is done via InteractableBase.RayTest (no Collider2D required).
/// Demonstrates mapping LockResult → InteractionFailReason.
/// </summary>
public class LockStateInteractable : InteractableBase
{
    public enum InteractMode { Toggle, Lock, Unlock }

    [Header("References")]
    [SerializeField] private LockState targetLockState;

    [Header("Behavior")]
    [SerializeField] private InteractMode mode = InteractMode.Toggle;

    private void Reset()
    {
        targetLockState = GetComponent<LockState>() ?? GetComponentInParent<LockState>(true);
    }

    protected override void ValidateExtra(bool isEditorPhase)
    {
        if (!targetLockState)
            targetLockState = GetComponent<LockState>() ?? GetComponentInParent<LockState>(true);

        if (!targetLockState && !isEditorPhase)
        {
            Debug.Log($"<color=red>[LockStateInteractable]</color> Missing LockState on '{name}'.");
        }
    }

    protected override bool DoInteract()
    {
        if (!targetLockState)
        {
            OnInteractionFailed(InteractionFailReason.Other);
            return false;
        }

        LockAction action = mode switch
        {
            InteractMode.Toggle => LockAction.Toggle,
            InteractMode.Lock => LockAction.Lock,
            InteractMode.Unlock => LockAction.Unlock,
            _ => LockAction.Toggle
        };

        var result = targetLockState.TryStateChange(action);

        switch (result)
        {
            case LockResult.Locked:
                Debug.Log("Locked.");
                return true;

            case LockResult.Unlocked:
                Debug.Log("Unlocked.");
                return true;

            case LockResult.AlreadyLocked:
            case LockResult.AlreadyUnlocked:
                Debug.Log("No change (already in desired state).");
                OnInteractionFailed(InteractionFailReason.AlreadyInDesiredState);
                return false;

            default:
                Debug.Log("Nothing happened.");
                OnInteractionFailed(InteractionFailReason.Other);
                return false;
        }
    }
}
