using Core.FSM;

namespace Game.Features.Characters.Hero.GrapplingHook {
    public enum HookState {
        Idle,
        Shooting,
        Attached
    }

    public class GrapplingHookFsm : SimpleStateMachine<HookState> {
        public GrapplingHookFsm() : base(HookState.Idle) {
            Permit(HookState.Idle, HookState.Shooting);
            Permit(HookState.Shooting, HookState.Attached);
            PermitFromAny(HookState.Idle); // forced abort from any state
        }
    }
}
