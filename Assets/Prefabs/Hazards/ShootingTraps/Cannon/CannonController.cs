using Core.Audio;
using Core.Components.Damage;
using Core.Components.GameObjects;
using Core.Services;
using Core.Utils;
using Prefabs.Characters.Common;
using UnityEngine;

namespace Prefabs.Hazards.ShootingTraps.Cannon {
    internal abstract class CannonAnimKeys {
        public static readonly int Fire = Animator.StringToHash("onFire");
        public static readonly int Hit = Animator.StringToHash("onHit");
        public static readonly int Dead = Animator.StringToHash("onDead");
    }
    
    public class CannonController : MonoBehaviour {
        [SerializeField]
        private SpawnComponent projectileSpawner;

        [SerializeField]
        private float shootCooldown = 2f;
        
        [Header("Sounds")]
        [SerializeField]
        private AudioCue shotSound;
        
        private Animator anim;

        private TinyTimer shootCooldownTimer;
        private Damageable damageable;

        private void Awake() {
            shootCooldownTimer = new TinyTimer(shootCooldown);
            
            damageable = GetComponent<Damageable>();
            
            anim = GetComponentInChildren<Animator>();
        }

        private void Update() {
            shootCooldownTimer.Update(Time.deltaTime);
        }

        public bool CanShoot() {
            return shootCooldownTimer.IsTimedOut;
        }
        
        public void Shoot() {
            if (!CanShoot()) {
                return;
            }

            shootCooldownTimer.Start();
            anim.SetTrigger(CannonAnimKeys.Fire);
            
            if (shotSound != null) {
                G.Audio.PlayAt(shotSound, transform.position);
            }
        }

        public void SpawnProjectile() {
            var projectile = projectileSpawner.SpawnInstance().GetComponent<ProjectileBase>();
            projectile.Direction = new Vector2(-transform.lossyScale.x, 0);
        }

        public void OnAfterHit() {
            if (!damageable.IsDead) {
                anim.SetTrigger(CannonAnimKeys.Hit);                
            }
        }

        public void OnDeath() {
            anim.SetTrigger(CannonAnimKeys.Dead);
        }

        public void DestroyAndSpawnDebris() {
            Debug.Log("Destroyed");
            GetComponent<SpawnComponent>().Spawn();
            Destroy(gameObject);
        }
    }
}
