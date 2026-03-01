using Core.FSM;

namespace Prefabs.Characters.PinkStar {
    public enum PinkyBehaviorState2 {
        // Idle - Doing nothing. Waiting for command/event.
        // Exit conditions:
        //   -> Anticipate - when the player is in vision.
        //   -> Hit - when hit.
        Idle,
        
        // Patrol - Move to another position
        // Exit conditions:
        //   -> Anticipate - when the player is in vision.
        //   -> Hit - when hit.
        Patrol,
        
        // Seeking - after being hit becomes hectic for a specific period of time.
        // Turns around to get player in vision.
        // Exit conditions:
        //   -> Idle - when player is not in vision after a specific period of time.
        Seeking,
    }
    
    public enum PinkyState {
        Calm,
        Anticipating,
        Attacking,
        Cooldown,
        Hit,
    }
    
    public interface IPinkySensors {
        bool IsAttackTriggered();
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
                .SetExitTime(1f);
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
    
    // public enum PinkyBehaviorState2 {
    //     // Idle - Doing nothing. Waiting for command/event.
    //     // Exit conditions:
    //     //   -> Anticipate - when the player is in vision.
    //     //   -> Hit - when hit.
    //     Idle,
    //     
    //     // Patrol - Move to another position
    //     // Exit conditions:
    //     //   -> Anticipate - when the player is in vision.
    //     //   -> Hit - when hit.
    //     Patrol,
    //     
    //     // Anticipate - Start attack/signal player
    //     // Exit conditions:
    //     //   -> Attacking - after a specific period of time.
    //     //   -> Hit - when hit.
    //     Anticipate,
    //     
    //     // Attacking - rolling over the area.
    //     // Exit conditions:
    //     //   -> Cooldown - after a specific period of time.
    //     Attack,
    //     
    //     // Hit - when hit. Loses control, doesn't do any actions.
    //     // Exit conditions:
    //     //   -> Seeking - after a specific period of time.
    //     Hit,
    //     
    //     // Seeking - after being hit becomes hectic for a specific period of time.
    //     // Turns around to get player in vision.
    //     // Exit conditions:
    //     //   -> Idle - when player is not in vision after a specific period of time.
    //     //   -> Anticipate - when player is in vision.
    //     Seeking,
    //     
    //     // Cooldown - after attacking, wait for cooldown period.
    //     //   -> Idle - when cooldown period is over.
    //     Cooldown,
    // }
    
    // public class PinkyStateMachine : SimpleStateMachine<PinkyBehaviorState2> {
    //     private IPinkySensors sensors;
    //
    //     public PinkyStateMachine(IPinkySensors sensors) : base(PinkyBehaviorState2.Idle) {
    //         this.sensors = sensors;
    //         
    //         
    //         // AddTransitions(PinkyState.Idle, PinkyState.Patrol, PinkyState.Wait);
    //         // AddTransitions(PinkyState.Patrol, PinkyState.Idle, PinkyState.Wait);
    //         // AddTransitions(PinkyState.Wait, PinkyState.Idle, PinkyState.Patrol);
    //     }
    // }
}
