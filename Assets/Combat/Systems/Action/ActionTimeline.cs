using UnityEngine;


namespace Combat
{
    public class ActionTimeline
    {
        public MoveDef Move { get; private set; }
        public int PhaseIndex { get; private set; } = -1;
        public bool IsRunning { get; private set; }
        public int ElapsedInPhaseMs { get; private set; }
        public uint ActionInstanceId { get; private set; }
        public ActionPhase CurrentPhaseId => (IsRunning && Move != null && Move.phases.Count > 0) ? Move.phases[PhaseIndex].phaseId : default;

        public bool CanStart => !IsRunning;
        public bool IsInPhase(ActionPhase phase) => IsRunning && CurrentPhaseId == phase;
        public bool HasValidMove => Move != null && Move.phases != null && Move.phases.Count > 0;


        public event System.Action<ActionPhase> OnPhaseEnter; public event System.Action<ActionPhase> OnPhaseExit; public event System.Action OnFinished;
        private static uint _instanceCounter = 1;


        public void Start(MoveDef move)
        {
            Move = move; ActionInstanceId = _instanceCounter++; PhaseIndex = -1; ElapsedInPhaseMs = 0; IsRunning = true; AdvancePhase();
        }


        public void Tick(int deltaMs)
        {
            if (!IsRunning || Move == null || Move.phases.Count == 0) return;
            ElapsedInPhaseMs += deltaMs; var phase = Move.phases[PhaseIndex];
            if (ElapsedInPhaseMs >= phase.durationMs) AdvancePhase();
        }


        public MoveDef.Phase GetCurrentPhaseOrNull()
        { if (!IsRunning || Move == null || PhaseIndex < 0 || PhaseIndex >= Move.phases.Count) return null; return Move.phases[PhaseIndex]; }


        private void AdvancePhase()
        {
            if (PhaseIndex >= 0 && PhaseIndex < Move.phases.Count) OnPhaseExit?.Invoke(Move.phases[PhaseIndex].phaseId);
            PhaseIndex++; ElapsedInPhaseMs = 0;
            if (PhaseIndex >= Move.phases.Count) { IsRunning = false; OnFinished?.Invoke(); return; }
            OnPhaseEnter?.Invoke(Move.phases[PhaseIndex].phaseId);
        }

        public void Cancel()
        {
            if (!IsRunning) return;

            // Exit current phase cleanly
            OnPhaseExit?.Invoke(CurrentPhaseId);

            // Stop running
            IsRunning = false;

            // Consider: a separate OnCanceled event (optional)
            OnFinished?.Invoke();
        }
    }
}