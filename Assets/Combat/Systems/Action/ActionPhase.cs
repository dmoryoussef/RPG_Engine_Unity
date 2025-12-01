// [Stage 2] New enum for authoring attack phases.
// [Stage 1] No impact; Stage 1 didn't model phases.
namespace Combat
{
    public enum ActionPhase
    {
        Startup = 0,
        Active = 1,
        Recovery = 2
    }
}
