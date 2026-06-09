using Core.Audio;
using Core.Utils;
using Game.Core.Bootstrap;
using Game.Core.Components.Base2D;
using Game.Core.Components.Damage;
using Game.Core.Components.GameObjects;
using Game.Core.Services.SceneState.Savers;
using Game.Core.Utils;
using Game.Features.Characters._Shared;
using Prefabs.Characters.Common;
using UnityEngine;

namespace Game.Features.Hazards.ShootingTraps.Cannon {
    internal abstract class CannonAnimKeys {
        public static readonly int Fire = Animator.StringToHash("onFire");
        public static readonly int Hit = Animator.StringToHash("onHit");
        public static readonly int Dead = Animator.StringToHash("onDead");
    }
    
    [RequireComponent(typeof(Facing2D))]
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
        private Facing2D facing;

        private void Awake() {
            shootCooldownTimer = new TinyTimer(shootCooldown);
            
            damageable = GetComponent<Damageable>();
            facing = GetComponent<Facing2D>();
            
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
            projectile.Direction = facing.DirVector;
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
            
            var destroyedSaver = GetComponent<DestructionStateSaver>();
            if (destroyedSaver != null) {
                destroyedSaver.MarkDestroyedAndDestroy();
            }
        }
    }
}
