using System.Collections;
using Core.Components.Collisions;
using Core.Services;
using Prefabs.Effects.InfoBubble;
using UnityEngine;

namespace Prefabs.Characters.Sharky {
    public enum SharkyBehaviorState {
        Idle,
        Patrolling,
        Chasing,
        Attacking,
        Anticipate,
        Aggro
    }

    [RequireComponent(typeof(SharkyController))]
    public class SharkyAI : BaseAI {
        [SerializeField]
        private LayerCheck vision;
        
        [SerializeField]
        private Transform infoBubblePoint;
        
        [SerializeField]
        private float agroDelay = 1f;
        
        private readonly float confusionDelay = 2f;
        private readonly float anticipationTime = 0.3f;
        private readonly float attackTime = 0.5f;
        
        private SharkyController ctrl;
        private GroundPatrolPath path;
        
        private SharkyBehaviorState behaviorState;
        private float prevX;
        
        private GameObject target;
        
        public SharkyBehaviorState BehaviorState => behaviorState;

        private void Awake() {
            ctrl = GetComponent<SharkyController>();
            path = GetComponent<GroundPatrolPath>();

            prevX = transform.position.x;
            behaviorState = SharkyBehaviorState.Patrolling;
        }

        private void OnEnable() {
            StartCoroutine(Init());
        }

        private IEnumerator Init() {
            // Skip one frame to let SharkyController initialize, as sometimes it's initialized after AI.
            yield return null;
            SetState(SharkyBehaviorState.Patrolling);
        }

        private void OnDisable() {
            SetState(SharkyBehaviorState.Idle);
        }

        private void SetState(SharkyBehaviorState newState) {
            behaviorState = newState;
            
            if (!enabled && newState != SharkyBehaviorState.Idle) {
                return;
            }
            
            switch (behaviorState) {
                case SharkyBehaviorState.Idle:
                    StopCurrentAction();
                    ctrl.StopMovement();
                    break;
                
                case  SharkyBehaviorState.Anticipate:
                    StartAction(Anticipate());
                    break;
                
                case SharkyBehaviorState.Chasing:
                    StartAction(ChaseHero());
                    break;

                case SharkyBehaviorState.Aggro:
                    StartAction(AgroToHero());
                    break;
                
                case SharkyBehaviorState.Attacking:
                    StartAction(Attack());
                    break;

                default:
                    StartAction(Patrolling());
                    break;
            }
        }

        private IEnumerator Patrolling() {
            target = null;

            while (true) {
                var point = path.GetTargetPoint();
                var targetPoint = point.position;

                if (ReachedOrPassed(targetPoint.x)) {
                    if (point.delay > 0) {
                        ctrl.StopMovement();
                        yield return new WaitForSeconds(point.delay);
                    }

                    path.NextTarget();
                } else {
                    ctrl.SetDirection(GetDirectionTowards(targetPoint));
                }

                yield return null;
            }
        }

        private IEnumerator AgroToHero() {
            // TODO: [BG] Separate AI and representation. Move all effects display to some additional layer.
            //   for now we'll just implement the easiest way.
            G.Spawner.SpawnInfoBubble(InfoBubbleType.Exclamation, infoBubblePoint.transform.position, transform);
            
            ctrl.StopMovement();
            
            yield return new WaitForSeconds(agroDelay);
            SetState(SharkyBehaviorState.Chasing);
        }

        private IEnumerator ChaseHero() {
            while (vision.IsColliding()) {
                ctrl.SetDirection(GetDirectionTowards(target.transform.position));
                yield return null;
            }

            // TODO: [BG] make confused
            yield return new WaitForSeconds(confusionDelay);
            
            SetState(SharkyBehaviorState.Patrolling);
        }
        
        public void OnHeroInVision(GameObject hero) {
            if (!enabled || ctrl.IsDead) {
                return;
            }

            target = hero;
            SetState(SharkyBehaviorState.Aggro);
        }

        // Attack area - area which actually deals damage.
        // Attack trigger - area, which triggers attack. It's usually smaller than attack area to have better chance
        //   to hit player before he runs away.
        public void OnHeroInAttackTrigger() {
            if (ctrl.CanAttack()) {
                SetState(SharkyBehaviorState.Anticipate);
            }
        }

        private IEnumerator Anticipate() {
            ctrl.Anticipate();
            yield return new WaitForSeconds(anticipationTime);
            SetState(SharkyBehaviorState.Attacking);
        }

        private IEnumerator Attack() {
            ctrl.Attack();
            yield return new WaitForSeconds(attackTime);
            SetState(SharkyBehaviorState.Chasing);
        }

        private bool ReachedOrPassed(float targetX) {
            var x = transform.position.x;

            // Returns true if our relative position to target point is changed between frames. 
            var crossed = (x - targetX) * (prevX - targetX) <= 0f;
            prevX = x;

            return crossed;
        }

        private Vector2 GetDirectionTowards(Vector2 targetPoint) {
            var x = transform.position.x;

            if (Mathf.Approximately(targetPoint.x, 0f)) {
                return Vector2.zero;
            }

            return targetPoint.x > x ? Vector2.right : Vector2.left;
        }
    }
}
