using System;
using System.Collections;
using UnityEngine;

namespace Prefabs.Characters.Sharky {
    [RequireComponent(typeof(SharkyController))]
    public class SharkyAI : MonoBehaviour {
        private SharkyController ctrl;
        private GroundPatrolPath path;

        private Coroutine activeCoroutine;

        private float prevX;
        
        private void Awake() {
            ctrl = GetComponent<SharkyController>();
            path = GetComponent<GroundPatrolPath>();
            
            prevX = transform.position.x;
        }

        private void Start() {
            StartAction(Patrolling());
        }

        private void OnDestroy() {
            StopCurrentAction();
        }

        // public void OnHeroInSight(GameObject go) {
        //     // 
        // }
        
        private IEnumerator Patrolling() {
            while (true) {
                var targetX = path.GetTargetPosition().x;

                if (ReachedOrPassed(targetX)) {
                    path.NextTarget();
                } else {
                    ctrl.SetDirection(GetDirectionTowards(targetX));                    
                }
                
                yield return null;
            }
        }

        private bool ReachedOrPassed(float targetX) {
            var x = transform.position.x;
            
            // Returns true if our relative position to target point is changed between frames. 
            var crossed = (x - targetX) * (prevX - targetX) <= 0f;
            prevX = x;
                
            return crossed;
        }

        private Vector2 GetDirectionTowards(float targetX) {
            var x = transform.position.x;

            if (Mathf.Approximately(targetX, 0f)) {
                return Vector2.zero;
            }
            
            return targetX > x ? Vector2.right : Vector2.left;
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
