using UnityEngine;
using State; 

namespace Interaction
{
    /// <summary>
    /// Generic interactable that forwards interaction to any attached State.IState.
    /// All cooldown / max uses / events / bounds / registry logic live in InteractableBase.
    /// </summary>
    public class InteractableComponent : InteractableBase
    {
        [Header("State Target")]
        [Tooltip("Component that implements State.IState (e.g. OpenCloseState, LockState). " +
                 "If not set, will auto-find on this GameObject.")]
        [SerializeField] private MonoBehaviour stateComponent;

        [Header("Input")]
        [Tooltip("Key used to trigger this interactable. Use None to disable direct key input.")]
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        public override KeyCode InteractionKey => interactionKey;

        private IState _state;

        // --------------------------------------------------
        // VALIDATION / RESOLUTION
        // --------------------------------------------------

        protected override void ValidateExtra(bool isEditorPhase)
        {
            // Reset validation to a good default; base.SoftValidate() has already done this.
            // We just refine the status based on the stateComponent.
            if (stateComponent == null)
            {
                // Auto-find on this GameObject
                _state = GetComponent<IState>();

                if (_state == null && !isEditorPhase)
                {
                    Debug.LogError(
                        $"[{nameof(InteractableComponent)}] No IState found on '{name}'. " +
                        "Assign a component that implements State.IState.",
                        this);
                }

                return;
            }

            _state = stateComponent as IState;
            if (_state == null)
            {
                if (!isEditorPhase)
                {
                    Debug.LogError(
                        $"[{nameof(InteractableComponent)}] Assigned stateComponent on '{name}' " +
                        $"does not implement State.IState.",
                        this);
                }
            }
        }

        // --------------------------------------------------
        // CORE INTERACTION
        // --------------------------------------------------

        protected override bool DoInteract()
        {
            if (_state == null)
            {
                OnInteractionFailed(InteractionFailReason.Other);
                return false;
            }

            var result = _state.TryStateChange();

            if (!string.IsNullOrEmpty(result.Message))
            {
                Debug.Log($"[Interact] {name}: {result.Message}", this);
            }

            if (!result.IsSuccess)
            {
                // We don't care *why* at this level – the message string is for UI/debug.
                OnInteractionFailed(InteractionFailReason.Other);
            }

            return result.IsSuccess;
        }
    }
}
