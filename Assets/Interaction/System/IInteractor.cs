
namespace Interaction
{
    /// <summary>Contract for anything that can perform interactions.</summary>
    public interface IInteractor
    {
        /// Attempts to interact with the currently best target (if any).
        bool TryInteract();

    }
}
