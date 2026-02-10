using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.FSM {
    /// <summary>
    /// Base non-generic state machine contract.
    /// </summary>
    public abstract class SimpleStateMachine {
        /// <summary>
        /// Updates the state machine timers and executes pending transitions.
        /// </summary>
        /// <param name="deltaTime">Frame delta time in seconds.</param>
        public abstract void Update(float deltaTime);
    }

    /// <summary>
    /// Enum-based finite state machine with immediate, delayed and exit-time transitions.
    /// </summary>
    /// <typeparam name="TState">State enum type.</typeparam>
    public class SimpleStateMachine<TState> : SimpleStateMachine where TState : Enum {
        public delegate void OnStateExitCallback(TState currentState, TState nextState);
        public delegate void OnStateEnterCallback(TState currentState, TState prevState);
        
        /// <summary>
        /// Current state.
        /// </summary>
        public TState State { get; private set; }
        
        public float TimeInState { get; private set; }

        /// <summary>
        /// Invoked after state enters a new value.
        /// </summary>
        public event OnStateExitCallback OnStateEnter;

        /// <summary>
        /// Invoked before state exits current value.
        /// </summary>
        public event OnStateEnterCallback OnStateExit;

        /// <summary>
        /// Progress (0..1) of currently pending delayed transition.
        /// Returns 0 when no delayed transition is pending.
        /// </summary>
        public float Progress {
            get {
                if (!pendingTransition.HasValue) {
                    return 0;
                }

                var delay = pendingTransition.Value.Delay;
                if (delay <= 0f) {
                    return 1f;
                }

                return Mathf.Clamp01(1f - timeToTransition / delay);
            }
        }

        private readonly Dictionary<TState, Dictionary<TState, StateTransition<TState>>> transitions =
            new Dictionary<TState, Dictionary<TState, StateTransition<TState>>>();

        private readonly Dictionary<TState, StateTransition<TState>> anyStateTransitions =
            new Dictionary<TState, StateTransition<TState>>();

        private DelayedTransitionData? pendingTransition;
        private float timeToTransition;

        /// <summary>
        /// Creates a state machine in initial state.
        /// </summary>
        /// <param name="initialState">Initial state value.</param>
        protected SimpleStateMachine(TState initialState) {
            State = initialState;
        }

        /// <summary>
        /// Adds transitions from a specific source state to provided target states.
        /// Throws when duplicate transition pair already exists.
        /// </summary>
        /// <param name="sourceState">Source state.</param>
        /// <param name="targetStates">Target states.</param>
        protected void AddTransitions(TState sourceState, params TState[] targetStates) {
            foreach (var toState in targetStates) {
                AddTransition(new StateTransition<TState>(sourceState, toState));
            }
        }

        protected StateTransition<TState> AddTransition(TState sourceState, TState targetState) {
            var transition = new StateTransition<TState>(sourceState, targetState);
            AddTransition(transition);
            return transition;
        }

        /// <summary>
        /// Adds transition definitions.
        /// Throws when duplicate transition pair already exists.
        /// </summary>
        /// <param name="trans">Transitions to add.</param>
        protected void AddTransitions(params StateTransition<TState>[] trans) {
            foreach (var transition in trans) {
                AddTransition(transition);
            }
        }

        /// <summary>
        /// Adds a transition definition.
        /// Throws when duplicate transition pair already exists.
        /// </summary>
        /// <param name="trans">Transition to add.</param>
        protected void AddTransition(StateTransition<TState> trans) {
            if (trans.FromState != null) {
                if (!transitions.TryGetValue(trans.FromState, out var fromTransitions)) {
                    fromTransitions = new Dictionary<TState, StateTransition<TState>>();
                    transitions.Add(trans.FromState, fromTransitions);
                }

                if (!fromTransitions.TryAdd(trans.ToState, trans)) {
                    throw new InvalidOperationException(
                        $"Transition {trans.FromState} -> {trans.ToState} already exists.");
                }

                return;
            }

            if (!anyStateTransitions.TryAdd(trans.ToState, trans)) {
                throw new InvalidOperationException(
                    $"Transition Any -> {trans.ToState} already exists.");
            }
        }

        /// <summary>
        /// Gets transition by exact source-target pair.
        /// </summary>
        /// <param name="fromState">Source state.</param>
        /// <param name="toState">Target state.</param>
        /// <returns>Transition or null when not found.</returns>
        public StateTransition<TState> GetTransition(TState fromState, TState toState) {
            if (transitions.TryGetValue(fromState, out var fromTransitions)
                && fromTransitions.TryGetValue(toState, out var transition)) {
                return transition;
            }

            return null;
        }

        /// <summary>
        /// Gets transition from any state to target state.
        /// </summary>
        /// <param name="toState">Target state.</param>
        /// <returns>Transition or null when not found.</returns>
        public StateTransition<TState> GetTransition(TState toState) {
            if (anyStateTransitions.TryGetValue(toState, out var transition)) {
                return transition;
            }

            return null;
        }

        /// <summary>
        /// Forcefully sets the current state ignoring transition rules.
        /// Exit and enter events are invoked when state actually changes.
        /// </summary>
        /// <param name="targetState">New state.</param>
        public void ResetTo(TState targetState) {
            SetState(targetState, true);
        }

        /// <summary>
        /// Checks whether current state can transition to target state.
        /// </summary>
        /// <param name="targetState">Target state.</param>
        /// <returns>True when transition is allowed.</returns>
        public bool CanGo(TState targetState) {
            if (EqualityComparer<TState>.Default.Equals(State, targetState)) {
                return false;
            }

            return GetTransition(State, targetState) != null || GetTransition(targetState) != null;
        }

        /// <summary>
        /// Performs immediate transition to target state if allowed.
        /// </summary>
        /// <param name="targetState">Target state.</param>
        /// <returns>True when transition was executed.</returns>
        public bool Go(TState targetState) {
            if (!CanGo(targetState)) {
                return false;
            }

            SetState(targetState, true);
            return true;
        }

        /// <summary>
        /// Schedules delayed transition to target state if allowed.
        /// </summary>
        /// <param name="delaySec">Delay in seconds.</param>
        /// <param name="targetState">Target state.</param>
        /// <returns>True when transition was scheduled.</returns>
        public bool GoLater(float delaySec, TState targetState) {
            if (!CanGo(targetState)) {
                return false;
            }

            pendingTransition = new DelayedTransitionData(targetState, delaySec);
            timeToTransition = delaySec;
            return true;
        }

        /// <summary>
        /// Updates pending timers and performs transition when delay expires.
        /// </summary>
        /// <param name="deltaTime">Frame delta time in seconds.</param>
        public override void Update(float deltaTime) {
            TimeInState += deltaTime;

            if (TryPerformConditionalTransition()) {
                return;
            }

            if (!pendingTransition.HasValue) {
                SetupExitTimeTransition();
            }

            if (pendingTransition.HasValue) {
                timeToTransition -= deltaTime;

                if (timeToTransition <= 0) {
                    Go(pendingTransition.Value.TargetState);
                }
            }
        }

        private void ResetPendingTransition() {
            pendingTransition = null;
            timeToTransition = 0;
        }

        private void SetState(TState newState, bool invokeEvents) {
            var oldState = State;
            var hasChanged = !EqualityComparer<TState>.Default.Equals(oldState, newState);

            if (hasChanged && invokeEvents) {
                OnStateExit?.Invoke(oldState, newState);
            }

            State = newState;
            TimeInState = 0;
            ResetPendingTransition();
            SetupExitTimeTransition();

            if (hasChanged && invokeEvents) {
                OnStateEnter?.Invoke(newState, oldState);
            }
        }

        private void SetupExitTimeTransition() {
            var exitTransition = GetEarliestExitTransition(State);
            if (exitTransition == null || !exitTransition.HasExitTime) {
                return;
            }

            var delay = Mathf.Max(0f, exitTransition.ExitTime);
            pendingTransition = new DelayedTransitionData(exitTransition.ToState, delay);
            timeToTransition = delay;
        }

        private StateTransition<TState> GetEarliestExitTransition(TState fromState) {
            StateTransition<TState> selectedTransition = null;
            var minExitTime = float.MaxValue;

            if (transitions.TryGetValue(fromState, out var fromTransitions)) {
                foreach (var transition in fromTransitions.Values) {
                    if (!transition.HasExitTime || transition.ExitTime >= minExitTime) {
                        continue;
                    }

                    minExitTime = transition.ExitTime;
                    selectedTransition = transition;
                }
            }

            return selectedTransition;
        }

        private bool TryPerformConditionalTransition() {
            if (!transitions.TryGetValue(State, out var fromTransitions)) {
                return false;
            }

            foreach (var transition in fromTransitions.Values) {
                if (transition.Condition == null || !transition.Condition(this)) {
                    continue;
                }

                return Go(transition.ToState);
            }

            return false;
        }

        private struct DelayedTransitionData {
            public readonly TState TargetState;
            public readonly float Delay;

            public DelayedTransitionData(TState targetState, float delay) {
                TargetState = targetState;
                Delay = delay;
            }
        }
    }
}
