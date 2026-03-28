using System;
using System.Collections.Generic;
using Core.FSM;
using NUnit.Framework;

namespace Game.Editor.Tests.FSM {
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
            Permit(TestState.B, TestState.A, TestState.C);
            Permit(TestState.A, TestState.C);
        }
    }

    public class TestStateMachineWithExitTime : SimpleStateMachine<TestState> {
        public TestStateMachineWithExitTime() : base(TestState.B) {
            PermitAfter(TestState.B, TestState.A, 0.5f);
            Permit(TestState.A, TestState.C);
        }
    }

    public class TestStateMachineWithAnyTransition : SimpleStateMachine<TestState> {
        public TestStateMachineWithAnyTransition() : base(TestState.B) {
            PermitFromAny(TestState.C);
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
            PermitIf(TestState.B, TestState.A, () => CanGoToA);
            PermitIf(TestState.B, TestState.C, () => CanGoToC);
        }
    }

    public class DuplicateTransitionsStateMachine : SimpleStateMachine<TestState> {
        public DuplicateTransitionsStateMachine() : base(TestState.B) {
        }

        public void AddDuplicate() {
            Permit(TestState.B, TestState.A);
            Permit(TestState.B, TestState.A);
        }
    }

    public class DuplicateAnyTransitionsStateMachine : SimpleStateMachine<TestState> {
        public DuplicateAnyTransitionsStateMachine() : base(TestState.B) {
        }

        public void AddDuplicateAny() {
            PermitFromAny(TestState.C);
            PermitFromAny(TestState.C);
        }
    }

    public class TestStateMachineWithZeroExitTime : SimpleStateMachine<TestState> {
        public TestStateMachineWithZeroExitTime() : base(TestState.B) {
            PermitAfter(TestState.B, TestState.A, 0f);
        }
    }

    public class TestStateMachineWithConditionAndExitTime : SimpleStateMachine<TestState> {
        public bool AllowTransition { get; set; }

        public TestStateMachineWithConditionAndExitTime() : base(TestState.B) {
            PermitIf(TestState.B, TestState.A, () => AllowTransition)
                .SetExitTime(0.5f);
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
        public void RejectsDuplicateAnyTransitions() {
            var duplicateFsm = new DuplicateAnyTransitionsStateMachine();

            Assert.Throws<InvalidOperationException>(() => duplicateFsm.AddDuplicateAny());
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
        public void AllowsAnyTransitionFromAnyState() {
            var anyTransitionFsm = new TestStateMachineWithAnyTransition();
            anyTransitionFsm.ResetTo(TestState.A);

            Assert.IsTrue(anyTransitionFsm.CanGo(TestState.C));
            Assert.IsTrue(anyTransitionFsm.Go(TestState.C));
            Assert.AreEqual(TestState.C, anyTransitionFsm.State);
        }

        [Test]
        public void FiresTransitionEventOnGo() {
            var events = new List<string>();
            fsm.OnTransition += (toState, fromState) => events.Add($"{fromState}->{toState}");

            fsm.Go(TestState.A);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("B->A", events[0]);
        }

        [Test]
        public void FiresTransitionEventOnResetTo() {
            var events = new List<string>();
            fsm.OnTransition += (toState, fromState) => events.Add($"{fromState}->{toState}");

            fsm.ResetTo(TestState.C);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("B->C", events[0]);
        }

        [Test]
        public void DoesNotFireTransitionEventWhenResetToSameState() {
            var events = new List<string>();
            fsm.OnTransition += (_, _) => events.Add("transition");

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
        public void CanGoNowEvaluatesConditions() {
            var conditionalFsm = new TestStateMachineWithConditions {
                CanGoToA = false
            };

            Assert.IsTrue(conditionalFsm.CanGo(TestState.A));
            Assert.IsFalse(conditionalFsm.CanGoNow(TestState.A));

            conditionalFsm.CanGoToA = true;
            Assert.IsTrue(conditionalFsm.CanGoNow(TestState.A));
        }

        [Test]
        public void CanGoNowRespectsExitTime() {
            var exitTimeFsm = new TestStateMachineWithExitTime();

            Assert.IsTrue(exitTimeFsm.CanGo(TestState.A));
            Assert.IsFalse(exitTimeFsm.CanGoNow(TestState.A));

            exitTimeFsm.Update(0.49f);
            Assert.IsFalse(exitTimeFsm.CanGoNow(TestState.A));
        }

        [Test]
        public void SupportsZeroExitTimeTransitions() {
            var zeroExitFsm = new TestStateMachineWithZeroExitTime();

            zeroExitFsm.Update(0f);

            Assert.AreEqual(TestState.A, zeroExitFsm.State);
        }

        [Test]
        public void RespectsExitTimeForConditionalTransitions() {
            var transitionFsm = new TestStateMachineWithConditionAndExitTime {
                AllowTransition = true
            };

            transitionFsm.Update(0.25f);
            Assert.AreEqual(TestState.B, transitionFsm.State);

            transitionFsm.Update(0.25f);
            Assert.AreEqual(TestState.A, transitionFsm.State);
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
