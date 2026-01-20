using System;
using UnityEngine;

namespace Prefabs.Characters.Common {
    public class Projectile : MonoBehaviour {
        [SerializeField]
        private float speed;

        private Rigidbody2D myRigidbody;
        private float directionScale;
        
        private void Start() {
            myRigidbody = GetComponent<Rigidbody2D>();
            
            // this should be initialized in Start, as in Awake lossyScale is not calculated yet.
            directionScale = transform.lossyScale.x > 0 ? 1 : -1; 
        }

        private void FixedUpdate() {
            var pos = myRigidbody.position;
            pos.x += speed * Time.fixedDeltaTime * directionScale;
            myRigidbody.MovePosition(pos);
        }
    }
}
