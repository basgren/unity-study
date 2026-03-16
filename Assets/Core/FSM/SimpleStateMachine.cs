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
        public delegate void StateTransitionCallback(TState toState, TState fromState);

        /// <summary>
        /// Current state.
        /// </summary>
        public TState State { get; private set; }

        public float TimeInState { get; private set; }

        public float TargetDuration {
            get {
                if (!pendingTransition.HasValue) {
                    return 0f;
                }

                return pendingTransition.Value.Delay;
            }
        }

        /// <summary>
        /// Invoked when state changes from one value to another.
        /// </summary>
        public event StateTransitionCallback OnTransition;

        /// <summary>
        /// Progress (0..1) of currently pending delayed transition.
        /// Returns 0 when no delayed transition is pending.
        /// </summary>
        public float Progress {
            get {
                if (!pendingTransition.HasValue) {
                    return 0f;
                }

                var delay = pendingTransition.Value.Delay;
                if (delay <= 0f) {
                    return 1f;
                }

                return Mathf.Clamp01(1f - timeToTransition / delay);
            }
        }

        private readonly Dictionary<TState, TransitionBucket> transitions = new();
        private readonly TransitionBucket anyStateTransitions = new();

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

        protected StateTransition<TState> AddAnyTransition(TState targetState) {
            var transition = new StateTransition<TState>(targetState);
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
            if (trans == null) {
                throw new ArgumentNullException(nameof(trans));
            }

            if (trans.HasFromState) {
                var fromTransitions = GetOrCreateTransitionBucket(trans.FromState);

                if (!fromTransitions.ByTarget.TryAdd(trans.ToState, trans)) {
                    throw new InvalidOperationException(
                        $"Transition {trans.FromState} -> {trans.ToState} already exists.");
                }

                fromTransitions.Ordered.Add(trans);
                return;
            }

            if (!anyStateTransitions.ByTarget.TryAdd(trans.ToState, trans)) {
                throw new InvalidOperationException(
                    $"Transition Any -> {trans.ToState} already exists.");
            }

            anyStateTransitions.Ordered.Add(trans);
        }

        /// <summary>
        /// Sugar API: equivalent to <see cref="AddTransition(TState, TState)"/>.
        /// </summary>
        protected StateTransition<TState> Permit(TState sourceState, TState targetState) {
            return AddTransition(sourceState, targetState);
        }

        /// <summary>
        /// Sugar API: equivalent to <see cref="AddTransitions(TState, TState[])"/>.
        /// </summary>
        protected void Permit(TState sourceState, params TState[] targetStates) {
            AddTransitions(sourceState, targetStates);
        }

        /// <summary>
        /// Sugar API: adds transition from any state to a specific target.
        /// </summary>
        protected StateTransition<TState> PermitFromAny(TState targetState) {
            return AddAnyTransition(targetState);
        }

        /// <summary>
        /// Sugar API: adds transitions from multiple source states to one target.
        /// </summary>
        protected void PermitFromMany(TState targetState, params TState[] sourceStates) {
            foreach (var sourceState in sourceStates) {
                AddTransition(sourceState, targetState);
            }
        }

        /// <summary>
        /// Sugar API: adds conditional transition with condition that does not require FSM argument.
        /// </summary>
        protected StateTransition<TState> PermitIf(TState sourceState, TState targetState, Func<bool> condition) {
            if (condition == null) {
                throw new ArgumentNullException(nameof(condition));
            }

            return AddTransition(sourceState, targetState).SetCondition(_ => condition());
        }

        /// <summary>
        /// Sugar API: adds conditional transition with full transition callback signature.
        /// </summary>
        protected StateTransition<TState> PermitIf(
            TState sourceState,
            TState targetState,
            StateTransition<TState>.StateTransitionCondition condition
        ) {
            return AddTransition(sourceState, targetState).SetCondition(condition);
        }

        /// <summary>
        /// Sugar API: adds same condition from multiple source states to one target.
        /// </summary>
        protected void PermitIfFromMany(TState targetState, Func<bool> condition, params TState[] sourceStates) {
            if (condition == null) {
                throw new ArgumentNullException(nameof(condition));
            }

            foreach (var sourceState in sourceStates) {
                AddTransition(sourceState, targetState).SetCondition(_ => condition());
            }
        }

        /// <summary>
        /// Sugar API: adds transition that can be taken after a specific time in source state.
        /// </summary>
        protected StateTransition<TState> PermitAfter(TState sourceState, TState targetState, float exitTimeSec) {
            return AddTransition(sourceState, targetState).SetExitTime(exitTimeSec);
        }

        /// <summary>
        /// Gets transition by exact source-target pair.
        /// </summary>
        /// <param name="fromState">Source state.</param>
        /// <param name="toState">Target state.</param>
        /// <returns>Transition or null when not found.</returns>
        public StateTransition<TState> GetTransition(TState fromState, TState toState) {
            return TryGetTransition(fromState, toState, out var transition)
                ? transition
                : null;
        }

        /// <summary>
        /// Gets transition from any state to target state.
        /// </summary>
        /// <param name="toState">Target state.</param>
        /// <returns>Transition or null when not found.</returns>
        public StateTransition<TState> GetTransition(TState toState) {
            return TryGetAnyTransition(toState, out var transition)
                ? transition
                : null;
        }

        /// <summary>
        /// Forcefully sets the current state ignoring transition rules.
        /// Transition event is invoked when state actually changes.
        /// </summary>
        /// <param name="targetState">New state.</param>
        public void ResetTo(TState targetState) {
            SetState(targetState, true);
        }

        /// <summary>
        /// Checks whether current state has any transition path to target state.
        /// Conditions and exit-time requirements are not evaluated.
        /// </summary>
        /// <param name="targetState">Target state.</param>
        /// <returns>True when transition route exists.</returns>
        public bool CanGo(TState targetState) {
            if (EqualityComparer<TState>.Default.Equals(State, targetState)) {
                return false;
            }

            return TryGetTransition(State, targetState, out _) || TryGetAnyTransition(targetState, out _);
        }

        /// <summary>
        /// Checks whether current state can transition to target state right now.
        /// Conditions and exit-time requirements are evaluated.
        /// </summary>
        /// <param name="targetState">Target state.</param>
        /// <returns>True when at least one available route is currently ready.</returns>
        public bool CanGoNow(TState targetState) {
            if (EqualityComparer<TState>.Default.Equals(State, targetState)) {
                return false;
            }

            if (TryGetTransition(State, targetState, out var fromTransition)
                && IsTransitionReadyNow(fromTransition)) {
                return true;
            }

            return TryGetAnyTransition(targetState, out var anyTransition)
                   && IsTransitionReadyNow(anyTransition);
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

            var delay = Mathf.Max(0f, delaySec);
            pendingTransition = new DelayedTransitionData(targetState, delay);
            timeToTransition = delay;
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

            if (!pendingTransition.HasValue) {
                return;
            }

            timeToTransition -= deltaTime;

            if (timeToTransition <= 0f) {
                Go(pendingTransition.Value.TargetState);
            }
        }

        private TransitionBucket GetOrCreateTransitionBucket(TState sourceState) {
            if (!transitions.TryGetValue(sourceState, out var bucket)) {
                bucket = new TransitionBucket();
                transitions.Add(sourceState, bucket);
            }

            return bucket;
        }

        private bool TryGetTransition(TState fromState, TState toState, out StateTransition<TState> transition) {
            if (transitions.TryGetValue(fromState, out var fromTransitions)
                && fromTransitions.ByTarget.TryGetValue(toState, out transition)) {
                return true;
            }

            transition = null;
            return false;
        }

        private bool TryGetAnyTransition(TState toState, out StateTransition<TState> transition) {
            return anyStateTransitions.ByTarget.TryGetValue(toState, out transition);
        }

        private bool IsTransitionReadyNow(StateTransition<TState> transition) {
            if (transition == null) {
                return false;
            }

            if (transition.HasExitTime && TimeInState < transition.ExitTime) {
                return false;
            }

            return transition.Condition == null || transition.Condition(this);
        }

        private void ResetPendingTransition() {
            pendingTransition = null;
            timeToTransition = 0f;
        }

        private void SetState(TState newState, bool invokeEvents) {
            var oldState = State;
            var hasChanged = !EqualityComparer<TState>.Default.Equals(oldState, newState);

            State = newState;
            TimeInState = 0f;
            ResetPendingTransition();
            SetupExitTimeTransition();

            if (hasChanged && invokeEvents) {
                OnTransition?.Invoke(newState, oldState);
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
            if (!transitions.TryGetValue(fromState, out var fromTransitions)) {
                return null;
            }

            StateTransition<TState> selectedTransition = null;
            var minExitTime = float.MaxValue;

            foreach (var transition in fromTransitions.Ordered) {
                if (!transition.HasExitTime || transition.Condition != null || transition.ExitTime >= minExitTime) {
                    continue;
                }

                minExitTime = transition.ExitTime;
                selectedTransition = transition;
            }

            return selectedTransition;
        }

        private bool TryPerformConditionalTransition() {
            if (TryPerformConditionalTransition(transitions.TryGetValue(State, out var fromTransitions)
                    ? fromTransitions
                    : null)) {
                return true;
            }

            return TryPerformConditionalTransition(anyStateTransitions);
        }

        private bool TryPerformConditionalTransition(TransitionBucket bucket) {
            if (bucket == null || bucket.Ordered.Count == 0) {
                return false;
            }

            foreach (var transition in bucket.Ordered) {
                if (transition.Condition == null || !IsTransitionReadyNow(transition)) {
                    continue;
                }

                return Go(transition.ToState);
            }

            return false;
        }

        private sealed class TransitionBucket {
            public readonly List<StateTransition<TState>> Ordered = new();
            public readonly Dictionary<TState, StateTransition<TState>> ByTarget = new();
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
