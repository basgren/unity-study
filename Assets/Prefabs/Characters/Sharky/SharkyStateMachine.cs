using Core.FSM;

namespace Prefabs.Characters.Sharky {
    public enum SharkyState {
        Idle,
        Patrol,
        Wait,
    }
    
    public class SharkyStateMachine : SimpleStateMachine<SharkyState> {
        public SharkyStateMachine() : base(SharkyState.Idle) {
            Permit(SharkyState.Idle, SharkyState.Patrol, SharkyState.Wait);
            Permit(SharkyState.Patrol, SharkyState.Idle, SharkyState.Wait);
            Permit(SharkyState.Wait, SharkyState.Idle, SharkyState.Patrol);
        }
    }
}
