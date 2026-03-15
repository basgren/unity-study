using System;
using Core.Audio;
using Core.Components.Animation;
using Core.Components.Damage;
using Core.Services;
using Core.Utils;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Prefabs.Hazards.ShootingTraps.Totem.Projectiles {
    [RequireComponent(typeof(MultiStateSpriteAnimator), typeof(Rigidbody2D))]
    public class SkullflameController : MonoBehaviour {
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

        private MultiStateSpriteAnimator anim;
        private Rigidbody2D rb;
        private HorzDirection2D direction;
        private float actualLifetime;
        private float lifetime;
        private bool isDying;
        private const float WallDetectorLength = 0.3f;
        private RaycastHit2D[] results = new RaycastHit2D[5];
        private IAudioLoopHandle flyingSoundHandle;

        private void Awake() {
            anim = GetComponent<MultiStateSpriteAnimator>();
            rb = GetComponent<Rigidbody2D>();
            SetHorzDirection(HorzDirection2D.Right);
            
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
                direction == HorzDirection2D.Right ? Vector2.right : Vector2.left,
                results,
                WallDetectorLength,
                wallLayer);
            
            if (count > 0) {
                FlipDirection();
            }
        }

        private void FlipDirection() {
            SetHorzDirection(direction == HorzDirection2D.Right ? HorzDirection2D.Left : HorzDirection2D.Right);
        }

        public void SetHorzDirection(HorzDirection2D dir) {
            direction = dir;
            UpdateSpeed();

            var ls = transform.localScale;
            transform.localScale = new Vector3(dir == HorzDirection2D.Right ? 1 : -1, ls.y, ls.z);
            Debug.Log($"Set scale: {transform.localScale}");
        }

        private void UpdateSpeed() {
            var speed = isDying ? 0f : linearSpeed;
            rb.velocity = new Vector2(
                direction == HorzDirection2D.Right ? speed : -speed,
                isDying ? 0 : rb.velocity.y
            );
        }

        public void OnHit(HitInfo hit) {
            StartDestroy();
        }

        private void StartDestroy() {
            anim.SetClip("destroy");
            isDying = true;
            
            if (flyingSoundHandle != null) {
                flyingSoundHandle.Stop();
                flyingSoundHandle = null;
            }

            if (destroySound != null) {
                G.Audio.PlayAt(destroySound, transform.position);
            }
        }
        
        public void OnDeath() {
            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected() {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(
                transform.position,
                transform.position + (direction == HorzDirection2D.Right ? Vector3.right : Vector3.left)
            );
        }
    }
}
