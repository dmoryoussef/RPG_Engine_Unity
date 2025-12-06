
namespace Targeting
{
    /// <summary>
    /// Describes a change in a specific targeting channel (Hover, Locked, Focus).
    /// </summary>
    public readonly struct FocusChange
    {
        public readonly FocusTarget Previous;
        public readonly FocusTarget Current;

        public FocusChange(FocusTarget previous, FocusTarget current)
        {
            Previous = previous;
            Current = current;
        }

        public override string ToString()
        {
            return $"{Previous?.TargetLabel ?? "null"} -> {Current?.TargetLabel ?? "null"}";
        }
    }
}
