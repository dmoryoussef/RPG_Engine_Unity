using Logging;
using System;
using UnityEngine;
using static UnityEditor.Timeline.TimelinePlaybackControls;

namespace State
{
    public enum StateStatus
    {
        Success,
        Failed,
        AlreadyInDesiredState,
        Blocked,        // generic "something prevented this"
    }

    /// <summary>
    /// Generic result of a state-change attempt, agnostic of specific systems.
    /// Message is a reason key or detail string ("opened", "locked", "jammed", etc.).
    /// </summary>
    public readonly struct StateResult
    {
        public readonly StateStatus Status;
        public readonly string Message;

        public bool IsSuccess => Status == StateStatus.Success;
        public bool IsAlreadyInState => Status == StateStatus.AlreadyInDesiredState;
        public bool IsBlocked => Status == StateStatus.Blocked;

        public StateResult(StateStatus status, string message = null)
        {
            Status = status;
            Message = message;
        }

        public static StateResult Succeed(string message = null)
            => new(StateStatus.Success, message);

        public static StateResult Fail(string message = null)
            => new(StateStatus.Failed, message);

        public static StateResult AlreadyInState(string message = null)
            => new(StateStatus.AlreadyInDesiredState, message);

        public static StateResult Blocked(string message = null)
            => new(StateStatus.Blocked, message);
    }

    /// <summary>
    /// "This component exposes a default state-change operation."
    /// Only states that should be driven by interaction implement this.
    /// </summary>
    public interface IState
    {
        StateResult TryStateChange(StateChangeContext context);
    }

    /// <summary>
    /// Shared base for interactable state components.
    /// Gives you debug, description, blocking, and a common interaction hook.
    /// Purely optional – non-interactable states don't need to inherit.
    /// </summary>
    public abstract class BaseState : MonoBehaviour, IState
    {
        [Header("State Debug")]
        [SerializeField] protected bool _debugLogging = true;

        [Header("Blocking")]
        [Tooltip("States that can block this state from changing.")]
        [SerializeField] protected BaseState[] _blockingStates;

        [Header("Interaction Behavior")]
        [SerializeField]
        [Tooltip("If true, this state may be triggered by input even when its interactable is not the current focus/target.")]
        private bool _allowStateChangeWhenNotTargeted = false;

        public bool AllowStateChangeWhenNotTargeted => _allowStateChangeWhenNotTargeted;

        /// <summary>
        /// Fired whenever this state runs its interaction-facing TryStateChange().
        /// </summary>
        public event Action<BaseState, StateResult> OnInteractionAttempted;

        /// <summary>
        /// Fired whenever the underlying state value changes in a way that 
        /// could affect description / inspection.
        /// </summary>
        public event Action<BaseState> OnStateChanged;

        public abstract StateResult TryStateChange(StateChangeContext context);


        /// <summary>
        /// A potential state change has become possible (e.g., actor entered range).
        /// Override to prepare or warm up any logic, prompts, or transient state.
        /// Default: no-op.
        /// </summary>
        public virtual void OnPreStateChangePotentialEntered(StateChangeContext context) { }

        /// <summary>
        /// The potential state change is no longer possible (e.g., actor left range).
        /// Override to undo/clear anything done in OnPreStateChangePotentialEntered.
        /// Default: no-op.
        /// </summary>
        public virtual void OnPreStateChangePotentialExited(StateChangeContext context) { }

        /// <summary>
        /// A state change has become more likely/imminent (e.g., gained focus/aimed at).
        /// Override to further prepare UI, highlighting, etc.
        /// Default: no-op.
        /// </summary>
        public virtual void OnPreStateChangeImminentEntered(StateChangeContext context) { }

        /// <summary>
        /// No longer imminent (e.g., lost focus/aim).
        /// Override to undo/clear anything done in OnPreStateChangeImminentEntered.
        /// Default: no-op.
        /// </summary>
        public virtual void OnPreStateChangeImminentExited(StateChangeContext context) { }


        public virtual string GetDescriptionText() => ToString();
        public virtual int GetDescriptionPriority() => 0;
        public virtual string GetDescriptionCategory() => null;

        public override string ToString() => base.ToString();

        /// <summary>
        /// Generic hook: ask all configured blocking states if they are blocking us.
        /// Returns Blocked(reasonKey) if any blocker says yes; otherwise Success().
        /// NOTE: no logging here — Report() handles standardized debug output.
        /// </summary>
        protected StateResult CheckBlockers()
        {
            if (_blockingStates == null || _blockingStates.Length == 0)
                return StateResult.Succeed();

            foreach (var other in _blockingStates)
            {
                if (!other) continue;

                if (other.IsBlocking(this, out var reasonKey))
                {
                    var message = string.IsNullOrEmpty(reasonKey) ? "blocked" : reasonKey;
                    return StateResult.Blocked(message);
                }
            }

            return StateResult.Succeed();
        }

        /// <summary>
        /// Override in states that can block other states.
        /// Default: this state never blocks anything.
        /// </summary>
        public virtual bool IsBlocking(BaseState target, out string reasonKey)
        {
            reasonKey = null;
            return false;
        }

        /// <summary>
        /// Standardized debug + event hook for ALL interaction attempts
        /// (success, already, failed, blocked).
        /// Call this at the end of TryStateChange() in derived classes.
        /// </summary>
        protected StateResult Report(StateResult result)
        {
            if (_debugLogging)
            {
                // System tag: "State"
                // Action: "TryStateChange"
                var status = result.Status.ToString();
                var message = result.Message;

                GameLog.Log(
                    this,
                    system: "State",
                    action: "TryStateChange",
                    result: status,
                    message: message);
            }

            OnInteractionAttempted?.Invoke(this, result);
            return result;
        }

        /// <summary>
        /// Call this in derived classes whenever the core state flips 
        /// (open/closed, locked/unlocked, etc.).
        /// </summary>
        protected void NotifyStateChanged()
        {
            OnStateChanged?.Invoke(this);
        }
    }
}
