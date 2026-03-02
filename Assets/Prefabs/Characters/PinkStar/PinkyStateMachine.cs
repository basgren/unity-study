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
            
            AddTransition(PinkyState.Calm, PinkyState.Anticipating)
                .SetCondition((_) => sensors.IsAttackTriggered());
            
            AddTransition(PinkyState.Anticipating, PinkyState.Attacking)
                .SetExitTime(0.5f);

            AddTransition(PinkyState.Attacking, PinkyState.Cooldown)
                .SetExitTime(3f);
                // .SetCondition((state) => state.TimeInState > 4f && sensors.IsGrounded());

            AddTransition(PinkyState.Cooldown, PinkyState.Calm)
                .SetExitTime(2f);
            
            AddTransition(PinkyState.Calm, PinkyState.Hit)
                .SetCondition(IsHit);
            
            AddTransition(PinkyState.Anticipating, PinkyState.Hit)
                .SetCondition(IsHit);
            
            AddTransition(PinkyState.Cooldown, PinkyState.Hit)
                .SetCondition(IsHit);
            
            AddTransition(PinkyState.Hit, PinkyState.Calm)
                .SetExitTime(1f);
        }

        private bool IsHit(SimpleStateMachine<PinkyState> stateMachine) {
            return sensors.IsHit();
        }
    }
}
