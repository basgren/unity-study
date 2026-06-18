using System;
using Core.Audio;
using Game.Core.Bootstrap;
using Game.Core.Components.Animation;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem.FallingStones {
    /// <summary>
    /// A stone dropped by the golem's Ground Hit. It falls under its own <see cref="Rigidbody2D"/>
    /// gravity (tune via project gravity / the body's gravity scale) and despawns when it overlaps the
    /// ground (<see cref="groundLayerMask"/>) or after <see cref="maxLifetime"/>, optionally spawning an
    /// impact effect. Player damage is handled by the sibling Damager — this component only owns the
    /// fall cleanup so spent stones don't pile up.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class FallingStone : MonoBehaviour {
        [SerializeField]
        [Tooltip("Layers that stop the stone (ground / floor). On overlap the stone despawns.")]
        private LayerMask groundLayerMask;

        [SerializeField]
        [Tooltip("Optional effect spawned at the stone's position when it lands or expires.")]
        private GameObject impactEffectPrefab;

        [SerializeField]
        [Tooltip("Safety despawn time if the stone never reaches ground.")]
        private float maxLifetime = 6f;

        [SerializeField]
        [Tooltip("Terminal fall speed in units/sec. 0 = unlimited.")]
        private float maxFallSpeed = 0f;

        [SerializeField]
        private GameObject damagerObject;

        [SerializeField]
        private AudioCue fallSound;

        

        private Rigidbody2D body;
        private float life;
        private bool despawned;
        private SimpleSpriteAnimator anim;

        private void Awake() {
            body = GetComponent<Rigidbody2D>();
            anim = GetComponent<SimpleSpriteAnimator>();
        }

        private void FixedUpdate() {
            if (maxFallSpeed <= 0f) {
                return;
            }

            // Clamp terminal velocity after gravity has been applied for this physics step.
            var velocity = body.linearVelocity;
            if (velocity.y < -maxFallSpeed) {
                velocity.y = -maxFallSpeed;
                body.linearVelocity = velocity;
            }
        }

        private void Update() {
            life += Time.deltaTime;
            if (life >= maxLifetime) {
                Despawn();
            }
        }

        private void OnCollisionEnter2D(Collision2D other) {
            // The stone's collider is a trigger, so it passes through the ground visually; detect the
            // overlap here and despawn at the contact point.
            if ((groundLayerMask.value & (1 << other.gameObject.layer)) != 0) {
                StartDespawn();
            }
        }

        public void StartDespawn() {
            if (despawned) {
                return;
            }

            despawned = true;
            
            damagerObject.SetActive(false);
            body.bodyType = RigidbodyType2D.Kinematic;
            anim.Play();
            G.Audio.PlayAt(fallSound, transform.position);
        }
        
        
        public void Despawn() {
            Destroy(gameObject);
        }
    }
}
