using Core.Audio;
using Core.Components.Base2D;
using Game.Core.Bootstrap;
using Game.Core.Components.Animation;
using Game.Core.Components.Damage;
using Game.Features.Characters._Shared;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Features.Hazards.ShootingTraps.Totem.Projectiles.Skullflame {
    [RequireComponent(typeof(MultiStateSpriteAnimator), typeof(Rigidbody2D), typeof(Facing2D))]
    public class SkullflameController : MonoBehaviour, IProjectileLifetime {
        [SerializeField]
        private float linearSpeed = 3f;

        [SerializeField]
        private float minLifetime = 13f;
        
        [SerializeField]
        private float maxLifetime = 15f;
        
        [SerializeField]
        private LayerMask wallLayer;
        
        [Header("Sounds")]
        [SerializeField]
        private AudioCue flyingSound;
        
        [SerializeField]
        private AudioCue destroySound;

        public float LifeTime {
            get => minLifetime;
            set {
                minLifetime = value;
                maxLifetime = value;
                UpdateActualLifetime();
            }
        }

        private MultiStateSpriteAnimator anim;
        private Rigidbody2D rb;
        private float actualLifetime;
        private float lifetime;
        private bool isDying;
        private const float WallDetectorLength = 0.3f;
        private readonly RaycastHit2D[] results = new RaycastHit2D[5];
        private IAudioLoopHandle flyingSoundHandle;
        private Facing2D facing;

        private void Awake() {
            anim = GetComponent<MultiStateSpriteAnimator>();
            rb = GetComponent<Rigidbody2D>();
            facing = GetComponent<Facing2D>();

            UpdateActualLifetime();
        }

        private void UpdateActualLifetime() {
            actualLifetime = Random.Range(minLifetime, maxLifetime);
        }

        private void Start() {
            if (flyingSound != null && flyingSoundHandle == null) {
                flyingSoundHandle = G.Audio.PlayLoopFollow(flyingSound, transform, is3D:true);
            }
        }

        private void FixedUpdate() {
            lifetime += Time.fixedDeltaTime;
            if (!isDying && lifetime > actualLifetime) {
                StartDestroy();
            }

            CheckObstacle();
            
            UpdateSpeed();
        }

        private void CheckObstacle() {
            var count = Physics2D.RaycastNonAlloc(
                transform.position,
                facing.DirVector,
                results,
                WallDetectorLength,
                wallLayer);
            
            if (count > 0) {
                facing.FlipDir();
            }
        }

        public void SetFacingDir(FacingDir dir) {
            facing.SetDir(dir);
            UpdateSpeed();
        }

        private void UpdateSpeed() {
            var speed = isDying ? 0f : linearSpeed;
            rb.velocity = new Vector2(
                speed * facing.DirSign,
                isDying ? 0 : rb.velocity.y
            );
        }

        public void OnHit(HitInfo hit) {
            StartDestroy();
        }

        private void StartDestroy() {
            anim.SetClip("destroy");
            isDying = true;

            StopFlyingSound();

            if (destroySound != null) {
                G.Audio.PlayAt(destroySound, transform.position);
            }
        }

        private void StopFlyingSound() {
            if (flyingSoundHandle != null) {
                flyingSoundHandle.Stop();
                flyingSoundHandle = null;
            }
        }
        
        public void OnDeath() {
            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected() {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(
                transform.position,
                transform.position + (facing.IsLeft ? Vector3.left : Vector3.right)
            );
        }
    }
}
