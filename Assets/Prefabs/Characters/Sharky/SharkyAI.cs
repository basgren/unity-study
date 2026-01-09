using System.Collections;
using Core.Components;
using UnityEngine;

namespace Prefabs.Characters.Sharky {
    enum SharkyBehaviorMode {
        Patrolling,
        Chasing,
        Attacking,
    }

    [RequireComponent(typeof(SharkyController))]
    public class SharkyAI : MonoBehaviour {
        [SerializeField]
        private LayerCheck vision;
        
        [SerializeField]
        private float agroDelay = 1f;
        
        private SharkyController ctrl;
        private GroundPatrolPath path;
        
        private SharkyBehaviorMode behaviorMode;
        private Coroutine activeCoroutine;
        private float prevX;
        
        private GameObject target;

        private void Awake() {
            ctrl = GetComponent<SharkyController>();
            path = GetComponent<GroundPatrolPath>();

            prevX = transform.position.x;
        }

        private void Start() {
            SetBehaviorMode(SharkyBehaviorMode.Patrolling);
        }

        private void SetBehaviorMode(SharkyBehaviorMode newMode) {
            behaviorMode = newMode;

            switch (behaviorMode) {
                case SharkyBehaviorMode.Chasing:
                    // TODO: [BG] implement
                    break;

                case SharkyBehaviorMode.Attacking:
                    // TODO: [BG] implement
                    break;

                default:
                    StartAction(Patrolling());
                    break;
            }
        }

        private void OnDestroy() {
            StopCurrentAction();
        }

        private IEnumerator Patrolling() {
            target = null;
            Debug.Log(">>>> PATROLLING");

            while (true) {
                var point = path.GetTargetPoint();
                var targetPoint = point.position;

                if (ReachedOrPassed(targetPoint.x)) {
                    if (point.delay > 0) {
                        ctrl.SetDirection(Vector2.zero);
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
            // TODO: [BG] Spawn exclamation
            yield return new WaitForSeconds(agroDelay);
            StartAction(GoToHero());
        }

        private IEnumerator GoToHero() {
            Debug.Log(">>>> GO TO HERO");
            while (vision.IsColliding()) {
                ctrl.SetDirection(GetDirectionTowards(target.transform.position));
                yield return null;
            }

            Debug.Log(">>> NOT COLLIDING");
            // TODO: [BG] make confused
            
            StartAction(Patrolling());
        }

        public void OnHeroInVision(GameObject hero) {
            target = hero;
            Debug.Log("Hero in sight");
            StartAction(AgroToHero());
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

        private void StartAction(IEnumerator action) {
            StopCurrentAction();

            activeCoroutine = StartCoroutine(action);
        }

        private void StopCurrentAction() {
            if (activeCoroutine == null) {
                return;
            }

            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }
    }
}
