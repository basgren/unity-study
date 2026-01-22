using System;
using UnityEngine;

namespace Prefabs.Characters.Common {
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour {
        [SerializeField]
        private float speed = 1f;

        [SerializeField]
        private bool invertDirection;

        private Rigidbody2D myRigidbody;
        private float directionScale;
        
        private void Start() {
            myRigidbody = GetComponent<Rigidbody2D>();
            
            // this should be initialized in Start, as in Awake lossyScale is not calculated yet.
            directionScale = transform.lossyScale.x > 0 ? 1 : -1;
        }

        private void FixedUpdate() {
            var pos = myRigidbody.position;
            var invert = invertDirection ? -1 : 1;
            pos.x += invert * speed * Time.fixedDeltaTime * directionScale;
            myRigidbody.MovePosition(pos);
        }
    }
}
