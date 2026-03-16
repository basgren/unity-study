using Core.FSM;

namespace Prefabs.Characters.PinkStar {
    public enum PinkyState {
        Calm,
        Anticipating,
        Attacking,
        Cooldown,
        Hit,
        Dead,
    }
    
    public interface IPinkySensors {
        bool IsAttackTriggered();
        bool IsPlayerInSight();
        bool IsGrounded();
        bool IsHit();
    }
    
    public class PinkyStateMachine : SimpleStateMachine<PinkyState> {
        private readonly IPinkySensors sensors;
        
        public PinkyStateMachine(PinkyState initialState, IPinkySensors sensors) : base(initialState) {
            this.sensors = sensors;
            
            PermitIf(PinkyState.Calm, PinkyState.Anticipating, () => sensors.IsAttackTriggered());
            
            PermitAfter(PinkyState.Anticipating, PinkyState.Attacking, 0.5f);

            PermitAfter(PinkyState.Attacking, PinkyState.Cooldown, 3f);
                // .SetCondition((state) => state.TimeInState > 4f && sensors.IsGrounded());

            PermitAfter(PinkyState.Cooldown, PinkyState.Calm, 2f);

            PermitIfFromMany(
                PinkyState.Hit,
                () => sensors.IsHit(),
                PinkyState.Calm,
                PinkyState.Anticipating,
                PinkyState.Cooldown
            );
            
            PermitAfter(PinkyState.Hit, PinkyState.Calm, 1f);
        }
    }
}
