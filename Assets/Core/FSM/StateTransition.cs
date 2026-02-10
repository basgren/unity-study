using System;
using JetBrains.Annotations;

namespace Core.FSM {
    /// <summary>
    /// Describes a state transition with optional exit time.
    /// </summary>
    /// <typeparam name="TState">State enum type.</typeparam>
    public class StateTransition<TState> where TState : Enum {
        public delegate bool StateTransitionCondition(SimpleStateMachine<TState> stateMachine);
        
        /// <summary>
        /// Exit time in seconds after entering the source state.
        /// </summary>
        public float ExitTime { get; private set; }

        /// <summary>
        /// True when this transition has an exit time greater than zero.
        /// </summary>
        public bool HasExitTime => ExitTime > 0f;

        /// <summary>
        /// True when transition has an explicit source state.
        /// </summary>
        public bool HasFromState { get; }

        /// <summary>
        /// Source state.
        /// </summary>
        [CanBeNull]
        public TState FromState { get; private set; }

        /// <summary>
        /// Target state.
        /// </summary>
        public TState ToState { get; private set; }

        [CanBeNull]
        public StateTransitionCondition Condition { get; private set; }

        /// <summary>
        /// Creates a transition from a specific state to a target state.
        /// </summary>
        /// <param name="fromState">Source state.</param>
        /// <param name="toState">Target state.</param>
        public StateTransition(TState fromState, TState toState) {
            HasFromState = true;
            FromState = fromState;
            ToState = toState;
        }

        /// <summary>
        /// Creates a transition from any state to a target state.
        /// </summary>
        /// <param name="toState">Target state.</param>
        public StateTransition(TState toState) {
            HasFromState = false;
            ToState = toState;
        }

        /// <summary>
        /// Sets transition exit time in seconds.
        /// </summary>
        /// <param name="time">Exit time in seconds.</param>
        /// <returns>Current transition for fluent configuration.</returns>
        public StateTransition<TState> SetExitTime(float time) {
            if (FromState == null) {
                throw new InvalidOperationException("Cannot set exit time for transitions from any state.");
            } 
            
            ExitTime = time;
            return this;
        }

        public StateTransition<TState> SetCondition(StateTransitionCondition func) {
            if (FromState == null) {
                throw new InvalidOperationException("Cannot set exit time for transitions from any state.");
            }

            Condition = func;
            return this;
        }
    }
}
