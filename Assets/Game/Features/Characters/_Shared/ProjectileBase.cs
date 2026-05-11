using Game.Core.Bootstrap;
using Game.Core.Services.Pool;
using UnityEngine;

namespace Game.Features.Characters._Shared {
    public interface IProjectileLifetime {
        float LifeTime { get; }
    }

    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class ProjectileBase : MonoBehaviour, IPoolable {
        [SerializeField]
        protected float linearSpeed = 1f;

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

        private Vector2 direction = Vector2.zero;
        public Vector2 Direction {
            get => direction;
            set => direction = value.normalized;
        }
        
        private Rigidbody2D myRigidbody;
        protected Rigidbody2D MyRigidbody => myRigidbody;
        private Vector2 prevCoord;
        private float travelledDistance;
        private float lifeTime;
        private Vector2 startPosition;

        private bool spawnInitialized;
        private bool despawned;

        protected virtual void Awake() {
            myRigidbody = GetComponent<Rigidbody2D>();
        }

        /// <summary>
        /// Resets per-spawn runtime state. Called by the pool after the instance is positioned
        /// and activated, and by <see cref="Start"/> as a fallback for scene-placed instances.
        /// </summary>
        public virtual void OnSpawn() {
            startPosition = myRigidbody.position;
            prevCoord = myRigidbody.position;
            travelledDistance = 0f;
            lifeTime = 0f;
            despawned = false;
            spawnInitialized = true;
        }

        public virtual void OnDespawn() {
            direction = Vector2.zero;
            spawnInitialized = false;
        }

        private void Start() {
            // Fallback for instances that bypass SpawnerService (e.g. placed in a scene).
            if (!spawnInitialized) {
                OnSpawn();
            }
        }

        private void FixedUpdate() {
            var pos = myRigidbody.position;

            pos = GetNewPosition(pos);

            myRigidbody.MovePosition(pos);

            travelledDistance += Vector2.Distance(pos, prevCoord);
            prevCoord = pos;

            if (travelledDistance > maxTravelDistance) {
                Despawn();
            }

            lifeTime += DeltaTime;
        }

        /// <summary>
        /// Returns this instance to its pool (or destroys it if not pooled).
        /// Idempotent: safe to call multiple times within the same frame.
        /// </summary>
        protected void Despawn() {
            // Both BreakSelf-style termination and the travel-distance cutoff can land in
            // the same FixedUpdate; pool's collectionCheck would throw on a double release.
            if (despawned) {
                return;
            }
            despawned = true;
            G.Spawner.Despawn(gameObject);
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
