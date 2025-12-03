using System;
using UnityEngine;

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
    /// </summary>
    public readonly struct StateResult
    {
        public readonly StateStatus Status;
        public readonly string Message; // state-specific text or key

        public bool IsSuccess => Status == StateStatus.Success;
        public bool IsAlreadyInState => Status == StateStatus.AlreadyInDesiredState;

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
        StateResult TryStateChange();
    }

    /// <summary>
    /// Shared base for interactable state components.
    /// Gives you debug and a common interaction hook.
    /// Purely optional – non-interactable states don't need to inherit.
    /// </summary>
    public abstract class BaseState : MonoBehaviour, IState
    {
        [Header("State Debug")]
        [SerializeField] protected bool _debugLogging = false;

        /// <summary>
        /// Fired whenever this state runs its interaction-facing TryStateChange().
        /// </summary>
        public event Action<BaseState, StateResult> OnInteractionAttempted;

        /// <summary>
        /// Fired whenever the underlying state value changes in a way that 
        /// could affect description / inspection.
        /// </summary>
        public event Action<BaseState> OnStateChanged;

        public abstract StateResult TryStateChange();

        public virtual string GetDescriptionText() => ToString();
        public virtual int GetDescriptionPriority() => 0;
        public virtual string GetDescriptionCategory() => null;

        public override string ToString() => base.ToString();

        protected StateResult Report(StateResult result)
        {
            if (_debugLogging && !string.IsNullOrEmpty(result.Message))
                Debug.Log($"[{GetType().Name}] {name}: {result.Message}", this);

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
