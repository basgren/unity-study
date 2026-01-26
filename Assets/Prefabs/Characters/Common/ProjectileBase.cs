using UnityEngine;

namespace Prefabs.Characters.Common {
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class ProjectileBase : MonoBehaviour {
        [SerializeField]
        protected float linearSpeed = 1f;

        [SerializeField]
        protected bool invertDirection;

        /// <summary>
        /// Maximum distance the projectile is allowed to travel. After travelling this distance, it will be destroyed.
        /// The distance is calculated along this trajectory (even for non-linear).
        /// </summary>
        [SerializeField]
        protected float maxTravelDistance = 100f;

        protected float DeltaTime => Time.fixedDeltaTime;
        
        /// Time since projectile was spawned.
        protected float LifeTime => lifeTime;
        protected Vector2 StartPosition => startPosition;
        
        protected float DirectionScale { get; private set; }
        
        private Rigidbody2D myRigidbody;
        private Vector2 prevCoord;
        private float travelledDistance;
        private float lifeTime;
        private Vector2 startPosition;

        private void Start() {
            myRigidbody = GetComponent<Rigidbody2D>();
            
            startPosition = myRigidbody.position;
            
            // This should be initialized in Start, as in Awake lossyScale is not calculated yet.
            DirectionScale = transform.lossyScale.x > 0 ? 1 : -1;
            prevCoord = myRigidbody.position;
        }

        private void FixedUpdate() {
            var pos = myRigidbody.position;

            pos = GetNewPosition(pos);
           
            myRigidbody.MovePosition(pos);

            travelledDistance += Vector2.Distance(pos, prevCoord);
            prevCoord = pos;

            if (travelledDistance > maxTravelDistance) {
                Destroy(gameObject);
            }
            
            lifeTime += DeltaTime;
        }

        /// <summary>
        /// Implement this method in descendant. It should return coords delta, which then will be used to move
        /// projectile in current frame.
        /// </summary>
        /// <param name="currentPos">Current position of the projectile.</param>
        /// <returns></returns>
        protected abstract Vector2 GetNewPosition(Vector2 currentPos);
    }
}
