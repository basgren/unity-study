using System.Collections;
using Core.Components.Collisions;
using Core.Services;
using Prefabs.Characters.Common;
using Prefabs.Characters.Sharky;
using Prefabs.Effects.InfoBubble;
using UnityEngine;

namespace Prefabs.Characters.PinkStar {
    public enum PinkyBehaviorState {
        Idle,
        Patrolling,
        Attacking,
        Anticipate,
        Aggro
    }

    [RequireComponent(typeof(PinkyController))]
    public class PinkyAI : BaseAI {
        [SerializeField]
        private LayerCheck vision;
        
        [SerializeField]
        private Transform infoBubblePoint;
        
        [SerializeField]
        private float agroDelay = 1f;
        
        private readonly float confusionDelay = 2f;
        private readonly float anticipationTime = 1f;
        private readonly float attackTime = 3f;
        
        private PinkyController ctrl;
        private GroundPatrolPath path;
        
        private PinkyBehaviorState behaviorState;
        private float prevX;
        
        private GameObject target;
        
        public PinkyBehaviorState BehaviorState => behaviorState;

        private void Awake() {
            ctrl = GetComponent<PinkyController>();
            path = GetComponent<GroundPatrolPath>();

            prevX = transform.position.x;
            behaviorState = PinkyBehaviorState.Patrolling;
        }

        private void OnEnable() {
            StartCoroutine(Init());
        }

        private IEnumerator Init() {
            // Skip one frame to let SharkyController initialize, as sometimes it's initialized after AI.
            yield return null;
            // SetState(PinkyBehaviorState.Patrolling);
        }

        private void OnDisable() {
            // SetState(PinkyBehaviorState.Idle);
        }

        // private void SetState(PinkyBehaviorState newState) {
        //     behaviorState = newState;
        //     
        //     if (!enabled && newState != PinkyBehaviorState.Idle) {
        //         return;
        //     }
        //     
        //     switch (behaviorState) {
        //         case PinkyBehaviorState.Idle:
        //             StopCurrentAction();
        //             ctrl.StopMovement();
        //             break;
        //         
        //         case  PinkyBehaviorState.Anticipate:
        //             StartAction(Anticipate());
        //             break;
        //
        //         case PinkyBehaviorState.Aggro:
        //             StartAction(AgroToHero());
        //             break;
        //         
        //         case PinkyBehaviorState.Attacking:
        //             StartAction(Attack());
        //             break;
        //
        //         default:
        //             StartAction(Patrolling());
        //             break;
        //     }
        // }
        //
        // private IEnumerator Patrolling() {
        //     target = null;
        //
        //     while (true) {
        //         var point = path.GetTargetPoint();
        //         var targetPoint = point.position;
        //
        //         if (ReachedOrPassed(targetPoint.x)) {
        //             if (point.delay > 0) {
        //                 ctrl.StopMovement();
        //                 yield return new WaitForSeconds(point.delay);
        //             }
        //
        //             path.NextTarget();
        //         } else {
        //             ctrl.SetDirection(GetDirectionTowards(targetPoint));
        //         }
        //
        //         yield return null;
        //     }
        // }
        //
        // private IEnumerator AgroToHero() {
        //     // TODO: [BG] Separate AI and representation. Move all effects display to some additional layer.
        //     //   for now we'll just implement the easiest way.
        //     // G.Spawner.SpawnInfoBubble(InfoBubbleType.Exclamation, infoBubblePoint.transform.position, transform);
        //     
        //     ctrl.StopMovement();
        //     
        //     yield return new WaitForSeconds(agroDelay);
        //     SetState(PinkyBehaviorState.Anticipate);
        // }
        //
        // private IEnumerator ChaseHero() {
        //     while (vision.IsColliding()) {
        //         ctrl.SetDirection(GetDirectionTowards(target.transform.position));
        //         yield return null;
        //     }
        //
        //     // TODO: [BG] make confused
        //     yield return new WaitForSeconds(confusionDelay);
        //     
        //     SetState(PinkyBehaviorState.Patrolling);
        // }
        //
        // public void OnHeroInVision(GameObject hero) {
        //     if (!enabled || ctrl.IsDead) {
        //         return;
        //     }
        //
        //     target = hero;
        //     SetState(PinkyBehaviorState.Aggro);
        // }
        //
        // // Attack area - area which actually deals damage.
        // // Attack trigger - area, which triggers attack. It's usually smaller than attack area to have better chance
        // //   to hit player before he runs away.
        // public void OnHeroInAttackTrigger() {
        //     // if (ctrl.CanAttack()) {
        //     //     SetState(PinkyBehaviorState.Anticipate);
        //     // }
        // }
        //
        // private IEnumerator Anticipate() {
        //     ctrl.StartAttack();
        //     yield return new WaitForSeconds(anticipationTime);
        //     SetState(PinkyBehaviorState.Attacking);
        // }
        //
        // private IEnumerator Attack() {
        //     ctrl.OnAttackStartedFrame();
        //     yield return new WaitForSeconds(attackTime);
        //     ctrl.EndAttackFrame();
        // }
        //
        // private bool ReachedOrPassed(float targetX) {
        //     var x = transform.position.x;
        //
        //     // Returns true if our relative position to target point is changed between frames. 
        //     var crossed = (x - targetX) * (prevX - targetX) <= 0f;
        //     prevX = x;
        //
        //     return crossed;
        // }
        //
        // private Vector2 GetDirectionTowards(Vector2 targetPoint) {
        //     var x = transform.position.x;
        //
        //     if (Mathf.Approximately(targetPoint.x, 0f)) {
        //         return Vector2.zero;
        //     }
        //
        //     return targetPoint.x > x ? Vector2.right : Vector2.left;
        // }
    }
}
