// [Stage 2] ActionTimelineController.cs
// Purpose: Starts & ticks the ActionTimeline. Can start with its own MoveDef OR
// auto-pick a co-located MeleeActionExecutor's MoveDef if none provided.
// Now exposes which executor is currently running.

using System.Linq;
using UnityEngine;

namespace Combat
{
    [AddComponentMenu("Combat/Action/Action Timeline Controller")]
    public sealed class ActionTimelineController : MonoBehaviour
    {
        [Header("Authoring (optional default)")]
        [Tooltip("Optional default action to start if none is passed in.")]
        public MoveDef moveDef;

        [Header("Fallback Discovery")]
        [Tooltip("If no MoveDef is provided, try to use a co-located MeleeActionExecutor's MoveDef.")]
        public bool autoPickLocalExecutor = true;

        [Header("Input (debug)")]
        public KeyCode attackKey = KeyCode.Mouse0;

        public ActionTimeline Timeline { get; private set; } = new ActionTimeline();

        [Header("Runtime (read-only)")]
        [SerializeField] private MoveDef _currentActionDef;
        [SerializeField] private MeleeActionExecutor _currentExecutor;

        public MoveDef CurrentActionDef => _currentActionDef;
        public MeleeActionExecutor CurrentExecutor => _currentExecutor;

        // Convenience props
        public uint ActionInstanceId => Timeline.ActionInstanceId;        // if you renamed to ActionInstanceId, adjust
        public ActionPhase CurrentPhase => Timeline.CurrentPhaseId;
        public int ElapsedInPhaseMs => Timeline.ElapsedInPhaseMs;

        // Pass-through events
        public event System.Action<ActionPhase> OnPhaseEnter { add { Timeline.OnPhaseEnter += value; } remove { Timeline.OnPhaseEnter -= value; } }
        public event System.Action<ActionPhase> OnPhaseExit { add { Timeline.OnPhaseExit += value; } remove { Timeline.OnPhaseExit -= value; } }
        public event System.Action OnFinished { add { Timeline.OnFinished += value; } remove { Timeline.OnFinished -= value; } }

        /// <summary>Primary start API. If <paramref name="def"/> is null, the controller will resolve one.</summary>
        public void StartAction(MoveDef def = null)
        {
            Resolve(def, out var resolvedDef, out var resolvedExec);

            if (resolvedDef == null || resolvedDef.phases == null || resolvedDef.phases.Count == 0)
            {
                Debug.LogWarning("[ActionTimelineController] No MoveDef with phases found on controller or local executors.", this);
                return;
            }

            _currentActionDef = resolvedDef;
            _currentExecutor = resolvedExec; // may be null if started from controller’s own def
            Timeline.Start(resolvedDef);
        }

        // Legacy alias if called elsewhere
        public void StartAttack() => StartAction(moveDef);

        private void Update()
        {
            // Debug input path
            if (Input.GetKeyDown(attackKey))
                StartAction(moveDef); // will auto-fallback if null

            // Tick timeline in ms (pipeline later can pass CombatFrame.DeltaMs)
            int deltaMs = Mathf.RoundToInt(Time.deltaTime * 1000f);
            Timeline.Tick(deltaMs);

            if (CurrentPhase == ActionPhase.Active)  // use AttackPhase if that’s your enum in MoveDef
            {
                var execs = GetComponents<MeleeActionExecutor>();
                var frame = new CombatFrame(Time.time, Time.deltaTime);
                foreach (var e in execs)
                {
                    if (!e.enabled || !e.gameObject.activeInHierarchy) continue;
                    // only drive ones that point to THIS controller
                    if (e.IsMyController(this))
                        e.ExecuteFrame(frame);
                }
            }
        }

        public MoveDef.Phase GetCurrentPhaseOrNull() => Timeline.GetCurrentPhaseOrNull();

        private void Resolve(MoveDef preferred, out MoveDef def, out MeleeActionExecutor exec)
        {
            // Priority 1: explicitly provided def
            if (preferred != null) { def = preferred; exec = null; return; }

            // Priority 2: controller’s own default
            if (moveDef != null) { def = moveDef; exec = null; return; }

            // Priority 3: first local executor with a valid def (if enabled)
            if (autoPickLocalExecutor)
            {
                var executors = GetComponents<MeleeActionExecutor>();
                foreach (var e in executors)
                {
                    var md = GetExecutorMoveDef(e);
                    if (md != null && md.phases != null && md.phases.Count > 0)
                    { 
                        def = md; 
                        exec = e; 
                        return; 
                    }
                }
            }

            // None found
            def = null; exec = null;
        }

        // Safe accessor into the executor's serialized field (or replace with a public getter on the executor)
        private static MoveDef GetExecutorMoveDef(MeleeActionExecutor exec)
        {
            var fi = typeof(MeleeActionExecutor)
                .GetField("moveDef", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return fi != null ? (MoveDef)fi.GetValue(exec) : null;
        }
    }
}
