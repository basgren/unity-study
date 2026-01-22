using System.Collections.Generic;
using Core.FSM;
using UnityEngine;

namespace Core.Services {
    /// <summary>
    /// Service for managing and updating state machines.
    /// It handles the lifecycle of state machines and ensures they are updated every frame.
    /// If the owner object is destroyed, the corresponding state machine is automatically removed.
    /// </summary>
    public class StateMachineService : MonoBehaviour {
        private readonly List<FsmEntry> entries = new List<FsmEntry>();

        void Update() {
            for (var i = entries.Count - 1; i >= 0; i--) {
                var entry = entries[i];

                if (entry.Owner == null || entry.StateMachine == null) {
                    entries.RemoveAt(i);
                    continue;
                }

                entry.StateMachine.Update(Time.deltaTime);
            }
        }

        /// <summary>
        /// Creates a new state machine and associates it with an owner.
        /// </summary>
        /// <typeparam name="TFsm">The type of the state machine to create.</typeparam>
        /// <param name="owner">The object that owns the state machine. If this object is destroyed,
        ///     the state machine will be released.</param>
        /// <returns>The newly created state machine instance.</returns>
        public TFsm Create<TFsm>(Object owner) where TFsm : SimpleStateMachine, new() {
            var newFsm = new TFsm();
            entries.Add(new FsmEntry { StateMachine = newFsm, Owner = owner });
            return newFsm;
        }

        private struct FsmEntry {
            public Object Owner;
            public SimpleStateMachine StateMachine;
        }
    }
}
