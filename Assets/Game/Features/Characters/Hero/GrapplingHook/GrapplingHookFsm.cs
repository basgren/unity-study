using Core.FSM;

namespace Game.Features.Characters.Hero.GrapplingHook {
    public enum HookState {
        Idle,
        Shooting,
        Attached,
        Retracting
    }

    public class GrapplingHookFsm : SimpleStateMachine<HookState> {
        public GrapplingHookFsm() : base(HookState.Idle) {
            Permit(HookState.Idle, HookState.Shooting);
            Permit(HookState.Shooting, HookState.Attached, HookState.Retracting);
            Permit(HookState.Attached, HookState.Retracting);
            Permit(HookState.Retracting, HookState.Idle);
            PermitFromAny(HookState.Idle); // forced abort from any state
        }
    }
}
