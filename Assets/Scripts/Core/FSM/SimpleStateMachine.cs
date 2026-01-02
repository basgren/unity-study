using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.FSM {
    public abstract class SimpleStateMachine {
        public abstract void Update(float deltaTime);
    }

    public class SimpleStateMachine<TState> : SimpleStateMachine where TState : Enum {
        public TState State { get; private set; }

        public float Progress {
            get {
                if (!pendingTransition.HasValue) {
                    return 0;
                }
                
                var delay = pendingTransition.Value.Delay;
                return Mathf.Clamp01(1f - timeToTransition / delay);
            }
        }

        private readonly Dictionary<TState, HashSet<TState>> transitions = new Dictionary<TState, HashSet<TState>>();
        private DelayedTransitionData? pendingTransition;
        private float timeToTransition;

        protected SimpleStateMachine(TState initialState) {
            State = initialState;
        }

        protected void AddTransitions(TState sourceState, params TState[] targetStates) {
            transitions.TryGetValue(sourceState, out var targets);

            foreach (var toState in targetStates) {
                if (targets == null) {
                    targets = new HashSet<TState>();
                    transitions.Add(sourceState, targets);
                }
                
                targets.Add(toState);
            }
        }

        public void ResetTo(TState targetState) {
            State = targetState;
        }
        
        public bool CanGo(TState targetState) {
            if (EqualityComparer<TState>.Default.Equals(State, targetState)) {
                return false;
            }

            return transitions.TryGetValue(State, out var targets)
                   && targets.Contains(targetState);
        }

        public bool Go(TState targetState) {
            if (!CanGo(targetState)) {
                return false;
            }

            State = targetState;
            ResetPendingTransition();
            return true;
        }
        
        public bool GoLater(float delaySec, TState targetState) {
            if (!CanGo(targetState)) {
                return false;
            }

            pendingTransition = new DelayedTransitionData(targetState, delaySec);
            timeToTransition = delaySec;
            return true;
        }

        public override void Update(float deltaTime) {
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
