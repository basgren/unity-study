using Core.FSM;

namespace Prefabs.Characters.Sharky {
    public enum SharkyState {
        Idle,
        Patrol,
        Wait,
    }
    
    public class SharkyStateMachine : SimpleStateMachine<SharkyState> {
        public SharkyStateMachine() : base(SharkyState.Idle) {
            AddTransitions(SharkyState.Idle, SharkyState.Patrol, SharkyState.Wait);
            AddTransitions(SharkyState.Patrol, SharkyState.Idle, SharkyState.Wait);
            AddTransitions(SharkyState.Wait, SharkyState.Idle, SharkyState.Patrol);
        }
    }
}
