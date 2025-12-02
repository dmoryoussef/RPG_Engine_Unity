
namespace Interaction
{
    /// <summary>Contract for anything that can perform interactions.</summary>
    public interface IInteractor
    {
        /// Attempts to pick a target given current settings (does not invoke it).
        bool TryPick(out InteractableBase target, out float distance);

        /// Attempts to interact with the currently best target (if any).
        bool TryInteract();

    }
}
