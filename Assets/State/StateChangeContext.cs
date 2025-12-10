using UnityEngine;

namespace State
{
    /// <summary>
    /// Generic trigger context for state changes. Does not expose any interaction-specific types.
    /// </summary>
    public struct StateChangeContext
    {
        /// <summary>
        /// The GameObject that initiated this state change (player, NPC, script, etc.)
        /// </summary>
        public GameObject Actor;

        /// <summary>
        /// The GameObject that owns this state.
        /// </summary>
        public GameObject Owner;

        /// <summary>
        /// Optional world position of the trigger.
        /// </summary>
        public Vector3? WorldPosition;

        /// <summary>
        /// Optional channel ("Interaction", "AI", "Script", etc.).
        /// </summary>
        public string Channel;

        /// <summary>
        /// Optional arbitrary tag.
        /// </summary>
        public string Tag;

        public static readonly StateChangeContext None = new StateChangeContext();
    }
}
