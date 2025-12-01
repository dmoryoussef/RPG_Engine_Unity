using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Shared frame data used by controller-driven and standalone execution.
    /// </summary>
    public readonly struct CombatFrame
    {
        public readonly float Time;       // Absolute time (seconds)
        public readonly float DeltaTime;  // Delta time (seconds)
        public readonly uint FrameIndex;  // Optional frame index (for replay)
        public int DeltaMs => Mathf.RoundToInt(DeltaTime * 1000f);

        public CombatFrame(float time, float deltaTime, uint frameIndex = 0)
        {
            Time = time;
            DeltaTime = deltaTime;
            FrameIndex = frameIndex;
        }
    }
}
