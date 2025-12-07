using UnityEngine;
using State;
using Logging;

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

        private IState _state;

        private const string SystemTag = "Interactable";

        /// <summary>
        /// Allow PlayerInteractor to query which key triggers this interactable.
        /// </summary>
        public override KeyCode InteractionKey => interactionKey;

        // --------------------------------------------------
        // VALIDATION / RESOLUTION
        // --------------------------------------------------

        /// <summary>
        /// Resolve the IState target and report configuration errors.
        /// </summary>
        protected override void ValidateExtra(bool isEditorPhase)
        {
            if (stateComponent == null)
            {
                if (_state == null && !isEditorPhase)
                {
                    GameLog.LogWarning(
                        this,
                        system: SystemTag,
                        action: "ValidateExtra",
                        message:
                            $"No IState found on '{name}'. " +
                            "Assign a component that implements State.IState.");
                }

                return;
            }

            _state = stateComponent as IState;
            if (_state == null && !isEditorPhase)
            {
                GameLog.LogError(
                    this,
                    system: SystemTag,
                    action: "ValidateExtra",
                    message:
                        $"Assigned stateComponent on '{name}' " +
                        "does not implement State.IState.");
            }
        }

        // --------------------------------------------------
        // CORE INTERACTION
        // --------------------------------------------------

        protected override bool DoInteract()
        {
            if (_state == null)
            {
                GameLog.LogWarning(
                    this,
                    system: SystemTag,
                    action: "DoInteract",
                    message: "Interaction attempted with no IState assigned.");

                OnInteractionFailed(InteractionFailReason.Other);
                return false;
            }

            var result = _state.TryStateChange();

            // Optional per-interaction state logs – controlled by the base debug flag.
            if (DebugLogging)
            {
                GameLog.Log(
                    this,
                    system: "Interact",
                    action: "DoInteract",
                    result: result.Status.ToString(),
                    message: result.Message);
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
