using System.Collections.Generic;
using Core.FSM;
using NUnit.Framework;

namespace Tests {
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
    }
}
