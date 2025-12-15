// ActionTimelineController.cs
// Purpose:
//  - Owns and ticks a single ActionTimeline instance.
//  - Maintains a registry of ActionExecutorBase components.
//  - Chooses which action to start by querying executors (WantsToStart).
//  - During Active, drives executors that share the current ActionId.
//  - No autofind, no default MoveDef, no controller-owned input bindings.

using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    [AddComponentMenu("Combat/Action/Action Timeline Controller")]
    public sealed class ActionTimelineController : MonoBehaviour
    {
        public ActionTimeline Timeline { get; private set; } = new ActionTimeline();

        [Header("Runtime (read-only)")]
        [SerializeField] private MoveDef _currentActionDef;
        [SerializeField] private string _currentActionId;

        public MoveDef CurrentActionDef => _currentActionDef;
        public string CurrentActionId => _currentActionId;

        // Convenience passthroughs
        public uint ActionInstanceId => Timeline.ActionInstanceId;
        public ActionPhase CurrentPhase => Timeline.CurrentPhaseId;
        public int ElapsedInPhaseMs => Timeline.ElapsedInPhaseMs;

        // Timeline events
        public event System.Action<ActionPhase> OnPhaseEnter
        {
            add => Timeline.OnPhaseEnter += value;
            remove => Timeline.OnPhaseEnter -= value;
        }

        public event System.Action<ActionPhase> OnPhaseExit
        {
            add => Timeline.OnPhaseExit += value;
            remove => Timeline.OnPhaseExit -= value;
        }

        public event System.Action OnFinished
        {
            add => Timeline.OnFinished += value;
            remove => Timeline.OnFinished -= value;
        }

        // ─────────────────────────────────────────────────────────────
        // Executor registry
        // ─────────────────────────────────────────────────────────────

        readonly List<ActionExecutorBase> _executors = new();

        public void RegisterExecutor(ActionExecutorBase exec)
        {
            if (exec == null) return;
            if (_executors.Contains(exec)) return;

            _executors.Add(exec);
        }

        public void UnregisterExecutor(ActionExecutorBase exec)
        {
            if (exec == null) return;
            _executors.Remove(exec);
        }

        // ─────────────────────────────────────────────────────────────
        // Update loop
        // ─────────────────────────────────────────────────────────────

        void Update()
        {
            // 1) If no action is running, ask executors if they want to start
            if (!Timeline.IsRunning)
            {
                TryStartFromExecutors();
            }

            // 2) Tick timeline
            int deltaMs = Mathf.RoundToInt(Time.deltaTime * 1000f);
            Timeline.Tick(deltaMs);

            // 3) Drive executors during Active
            if (CurrentPhase == ActionPhase.Active)
            {
                DriveActiveExecutors();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Action start logic
        // ─────────────────────────────────────────────────────────────

        void TryStartFromExecutors()
        {
            for (int i = 0; i < _executors.Count; i++)
            {
                var exec = _executors[i];
                if (exec == null || !exec.enabled || !exec.gameObject.activeInHierarchy)
                    continue;

                if (!exec.IsMyController(this))
                    continue;

                if (!exec.WantsToStart())
                    continue;

                var def = exec.GetMoveDefToStart();
                if (def == null || def.phases == null || def.phases.Count == 0)
                {
                    Debug.LogWarning(
                        $"[ActionTimelineController] Executor '{exec.name}' wants to start but has no valid MoveDef.",
                        this);
                    continue;
                }

                // If something is already running, only start if this phase allows interrupts.
                if (Timeline.IsRunning)
                {
                    if (!CanInterruptWith(exec.ActionId))
                        return; // running + cannot interrupt right now → ignore this start attempt

                    Timeline.Cancel(); // your ActionTimeline already has Cancel() :contentReference[oaicite:4]{index=4}
                }

                _currentActionDef = def;
                _currentActionId = exec.ActionId;

                Timeline.Start(def);
                return;
            }
        }

        bool CanInterruptWith(string nextActionId)
        {
            var phaseDef = GetCurrentPhaseOrNull();
            if (phaseDef == null) return false;

            switch (phaseDef.interruptPolicy)
            {
                case MoveDef.Phase.InterruptPolicy.None:
                    return false;

                case MoveDef.Phase.InterruptPolicy.Any:
                    return true;

                case MoveDef.Phase.InterruptPolicy.Whitelist:
                    if (phaseDef.interruptWhitelistActionIds == null) return false;
                    for (int i = 0; i < phaseDef.interruptWhitelistActionIds.Length; i++)
                    {
                        if (phaseDef.interruptWhitelistActionIds[i] == nextActionId)
                            return true;
                    }
                    return false;

                default:
                    return false;
            }
        }


        // ─────────────────────────────────────────────────────────────
        // Execution during Active
        // ─────────────────────────────────────────────────────────────

        void DriveActiveExecutors()
        {
            var frame = new CombatFrame(Time.time, Time.deltaTime);
            int driven = 0;

            for (int i = 0; i < _executors.Count; i++)
            {
                var exec = _executors[i];
                if (exec == null || !exec.enabled || !exec.gameObject.activeInHierarchy)
                    continue;

                if (!exec.IsMyController(this))
                    continue;

                if (exec.ActionId != _currentActionId)
                    continue;

                exec.ExecuteFrame(frame);
                driven++;
            }

            if (driven == 0)
            {
                Debug.LogWarning(
                    $"[ActionTimelineController] Action '{_currentActionId}' is Active but no executors were driven. " +
                    "Check executor ActionId values and controller wiring.",
                    this);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Timeline helpers
        // ─────────────────────────────────────────────────────────────

        public MoveDef.Phase GetCurrentPhaseOrNull()
        {
            return Timeline.GetCurrentPhaseOrNull();
        }

        void OnEnable()
        {
            Timeline.OnFinished += HandleFinished;
        }

        void OnDisable()
        {
            Timeline.OnFinished -= HandleFinished;
        }

        void HandleFinished()
        {
            _currentActionDef = null;
            _currentActionId = null;
        }
    }
}
