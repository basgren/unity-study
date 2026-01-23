using System;
using UnityEngine;

namespace Prefabs.Characters.Common {
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour {
        [SerializeField]
        private float speed = 1f;

        [SerializeField]
        private bool invertDirection;

        /// <summary>
        /// Maximum distance the projectile is allowed to travel. After travelling this distance, it will be destroyed.
        /// The distance is calculated along this trajectory (even for non-linear).
        /// </summary>
        [SerializeField]
        private float maxTravelDistance = 100f;

        private Rigidbody2D myRigidbody;
        private float directionScale;

        private Vector2 prevCoord;
        private float travelledDistance;
        
        private void Start() {
            myRigidbody = GetComponent<Rigidbody2D>();
            
            // this should be initialized in Start, as in Awake lossyScale is not calculated yet.
            directionScale = transform.lossyScale.x > 0 ? 1 : -1;
            prevCoord = myRigidbody.position;
        }

        private void FixedUpdate() {
            var pos = myRigidbody.position;
            var invert = invertDirection ? -1 : 1;
            pos.x += invert * speed * Time.fixedDeltaTime * directionScale;
            myRigidbody.MovePosition(pos);
            
            travelledDistance += Vector2.Distance(pos, prevCoord);
            prevCoord = pos;

            if (travelledDistance > maxTravelDistance) {
                Destroy(gameObject);
            }
        }
    }
}
