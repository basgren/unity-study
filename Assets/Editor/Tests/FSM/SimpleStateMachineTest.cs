using System;
using System.Collections.Generic;
using Core.FSM;
using NUnit.Framework;

namespace Editor.Tests.FSM {
    public enum TestState {
        A, // Can go only to C
        B, // Initial state, can go to A and C
        C, // Final state, cannot go anywhere
    }

    public class TestStateMachine : SimpleStateMachine<TestState> {
        public TestStateMachine() : base(TestState.B) {
            // Rules:
            //   B -> A, C
            //   A -> C
            //   C -> final state
            AddTransitions(TestState.B, TestState.A, TestState.C);
            AddTransitions(TestState.A, TestState.C);
        }
    }

    public class TestStateMachineWithExitTime : SimpleStateMachine<TestState> {
        public TestStateMachineWithExitTime() : base(TestState.B) {
            AddTransitions(
                new StateTransition<TestState>(TestState.B, TestState.A)
                    .SetExitTime(0.5f),
                new StateTransition<TestState>(TestState.A, TestState.C)
            );
        }
    }

    public class TestStateMachineWithAnyTransition : SimpleStateMachine<TestState> {
        public TestStateMachineWithAnyTransition() : base(TestState.B) {
            AddTransitions(new StateTransition<TestState>(TestState.C));
        }

        public StateTransition<TestState> FindFromTo(TestState fromState, TestState toState) {
            return GetTransition(fromState, toState);
        }

        public StateTransition<TestState> FindAnyTo(TestState toState) {
            return GetTransition(toState);
        }
    }

    public class TestStateMachineWithConditions : SimpleStateMachine<TestState> {
        public bool CanGoToA { get; set; }
        public bool CanGoToC { get; set; }

        public TestStateMachineWithConditions() : base(TestState.B) {
            AddTransitions(
                new StateTransition<TestState>(TestState.B, TestState.A)
                    .SetCondition(stateMachine => ((TestStateMachineWithConditions)stateMachine).CanGoToA),
                new StateTransition<TestState>(TestState.B, TestState.C)
                    .SetCondition(stateMachine => ((TestStateMachineWithConditions)stateMachine).CanGoToC)
            );
        }
    }

    public class DuplicateTransitionsStateMachine : SimpleStateMachine<TestState> {
        public DuplicateTransitionsStateMachine() : base(TestState.B) {
        }

        public void AddDuplicate() {
            AddTransitions(TestState.B, TestState.A);
            AddTransitions(TestState.B, TestState.A);
        }
    }

    public class SimpleStateMachineTest {
        private TestStateMachine fsm;

        [SetUp]
        public void Setup() {
            fsm = new TestStateMachine();
        }

        [Test]
        public void SetsDefaultStateDuringCreation() {
            Assert.AreEqual(fsm.State, TestState.B);
        }

        [Test]
        public void AllowsResettingToAnyStateIgnoringRules() {
            fsm.ResetTo(TestState.C);
            Assert.AreEqual(fsm.State, TestState.C);

            fsm.ResetTo(TestState.A);
            Assert.AreEqual(fsm.State, TestState.A);

            fsm.ResetTo(TestState.B);
            Assert.AreEqual(fsm.State, TestState.B);
        }

        [Test]
        public void ReturnsProperPermissionsForSpecificTransition() {
            fsm.ResetTo(TestState.B);
            Assert.IsTrue(fsm.CanGo(TestState.A), "can B -> A");
            Assert.IsTrue(fsm.CanGo(TestState.C), "can B -> C");
            Assert.IsFalse(fsm.CanGo(TestState.B), "cannot B -> B"); // Not allowed to go to the same state

            fsm.ResetTo(TestState.A);
            Assert.IsTrue(fsm.CanGo(TestState.C), "can A -> C");
            Assert.IsFalse(fsm.CanGo(TestState.B), "cannot A -> B");

            fsm.ResetTo(TestState.C);
            Assert.IsFalse(fsm.CanGo(TestState.A), "cannot C -> A");
            Assert.IsFalse(fsm.CanGo(TestState.B), "cannot C -> B");
        }

        [Test]
        public void GoesToAllowedStatesAndReturnsResult() {
            fsm.ResetTo(TestState.B);
            Assert.IsTrue(fsm.Go(TestState.A), "performs allowed transition");
            Assert.AreEqual(fsm.State, TestState.A, "state is changed");

            Assert.IsFalse(fsm.Go(TestState.B), "doesn't allow transition");
            Assert.AreEqual(fsm.State, TestState.A, "keeps state");
        }

        // ---=== Delayed Transition ===---

        [Test]
        public void PerformsDelayedTransition() {
            fsm.GoLater(0.5f, TestState.A);
            fsm.Update(0.25f);

            Assert.AreEqual(fsm.State, TestState.B, "state is still not changed");

            fsm.Update(0.25f);

            Assert.AreEqual(fsm.State, TestState.A, "state is changed with delay");
        }

        [Test]
        public void ImmediatelyReturnsFalseIfDelayedTransitionIsProhibited() {
            fsm.ResetTo(TestState.C);

            Assert.IsFalse(fsm.GoLater(0.5f, TestState.A));
            fsm.Update(0.5f);
            Assert.AreEqual(fsm.State, TestState.C);
        }

        [Test]
        public void ReturnsProgressForPendingState() {
            fsm.GoLater(0.5f, TestState.A);
            fsm.Update(0.1f);

            Assert.AreEqual(fsm.Progress, 0.2f, 1e-5f);
        }

        [Test]
        public void RejectsDuplicateFromToTransitions() {
            var duplicateFsm = new DuplicateTransitionsStateMachine();

            Assert.Throws<InvalidOperationException>(() => duplicateFsm.AddDuplicate());
        }

        [Test]
        public void PerformsTransitionByExitTime() {
            var exitTimeFsm = new TestStateMachineWithExitTime();

            exitTimeFsm.Update(0.25f);
            Assert.AreEqual(TestState.B, exitTimeFsm.State);

            exitTimeFsm.Update(0.25f);
            Assert.AreEqual(TestState.A, exitTimeFsm.State);
        }

        [Test]
        public void GetsTransitionByFromToAndByAnyTo() {
            var anyTransitionFsm = new TestStateMachineWithAnyTransition();

            Assert.IsNull(anyTransitionFsm.FindFromTo(TestState.B, TestState.C));
            Assert.IsNotNull(anyTransitionFsm.FindAnyTo(TestState.C));
            Assert.IsNotNull(fsm.GetTransition(TestState.B, TestState.A));
        }

        [Test]
        public void FiresExitThenEnterEventsOnGo() {
            var events = new List<string>();
            fsm.OnStateExit += (state, _) => events.Add($"exit:{state}");
            fsm.OnStateEnter += (state, _) => events.Add($"enter:{state}");

            fsm.Go(TestState.A);

            Assert.AreEqual(2, events.Count);
            Assert.AreEqual("exit:B", events[0]);
            Assert.AreEqual("enter:A", events[1]);
        }

        [Test]
        public void FiresExitThenEnterEventsOnResetTo() {
            var events = new List<string>();
            fsm.OnStateExit += (state, _) => events.Add($"exit:{state}");
            fsm.OnStateEnter += (state, _) => events.Add($"enter:{state}");

            fsm.ResetTo(TestState.C);

            Assert.AreEqual(2, events.Count);
            Assert.AreEqual("exit:B", events[0]);
            Assert.AreEqual("enter:C", events[1]);
        }

        [Test]
        public void DoesNotFireEventsWhenResetToSameState() {
            var events = new List<string>();
            fsm.OnStateExit += (state, _) => events.Add($"exit:{state}");
            fsm.OnStateEnter += (state, _) => events.Add($"enter:{state}");

            fsm.ResetTo(TestState.B);

            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void PerformsConditionalTransitionOnUpdate() {
            var conditionalFsm = new TestStateMachineWithConditions {
                CanGoToA = true
            };

            conditionalFsm.Update(0.1f);

            Assert.AreEqual(TestState.A, conditionalFsm.State);
        }

        [Test]
        public void FirstMatchingConditionalTransitionWins() {
            var conditionalFsm = new TestStateMachineWithConditions {
                CanGoToA = true,
                CanGoToC = true
            };

            conditionalFsm.Update(0.1f);

            Assert.AreEqual(TestState.A, conditionalFsm.State);
        }

        [Test]
        public void KeepsStateWhenNoConditionalTransitionMatches() {
            var conditionalFsm = new TestStateMachineWithConditions();

            conditionalFsm.Update(0.1f);

            Assert.AreEqual(TestState.B, conditionalFsm.State);
        }

        [Test]
        public void TracksTimeInStateAcrossUpdates() {
            fsm.Update(0.25f);
            fsm.Update(0.5f);

            Assert.AreEqual(0.75f, fsm.TimeInState, 1e-5f);
        }

        [Test]
        public void ResetsTimeInStateAfterTransition() {
            fsm.Update(0.25f);

            Assert.AreEqual(0.25f, fsm.TimeInState, 1e-5f);

            fsm.Go(TestState.A);

            Assert.AreEqual(0f, fsm.TimeInState, 1e-5f);
        }
    }
}
