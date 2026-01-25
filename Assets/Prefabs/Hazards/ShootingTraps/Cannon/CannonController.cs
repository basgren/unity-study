using Core.Components.Damage;
using Core.Components.GameObjects;
using Core.Utils;
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
        }

        public void SpawnProjectile() {
            projectileSpawner.Spawn();
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
