using Core.Audio;
using Core.Components.Base2D;
using Core.Components.Damage;
using Core.Components.GameObjects;
using Core.Services;
using Prefabs.Characters.Common;
using Prefabs.Hazards.ShootingTraps.Totem.Projectiles.GiantFly;
using Prefabs.Hazards.ShootingTraps.Totem.Projectiles.Skullflame;
using UnityEngine;

namespace Prefabs.Hazards.ShootingTraps.Common {
    internal abstract class SimpleShooterAnimKeys {
        public static readonly int Fire = Animator.StringToHash("onFire");
        public static readonly int Hit = Animator.StringToHash("onHit");
        public static readonly int Dead = Animator.StringToHash("onDead");
    }

    [RequireComponent(typeof(Damageable))]
    [RequireComponent(typeof(Facing2D))]
    public class SimpleShooter : MonoBehaviour {
        [SerializeField]
        private SpawnComponent projectileSpawner;
        
        [SerializeField]
        // If projectile supports lifetime setting, it will be set.
        private float projectileLifetime = 5f;

        [Header("Sounds")]
        [SerializeField]
        private AudioCue shotSound;

        private Animator anim;

        private Damageable damageable;
        private Facing2D facing;

        private void Awake() {
            damageable = GetComponent<Damageable>();
            facing = GetComponent<Facing2D>();

            anim = GetComponentInChildren<Animator>();
        }

        public void Shoot() {
            anim.SetTrigger(SimpleShooterAnimKeys.Fire);

            if (shotSound != null) {
                G.Audio.PlayAt(shotSound, transform.position);
            }
        }

        public void SpawnProjectile() {
            // TODO: [BG] Refactor it. Direction should be applied either in spawn or in own controllers.
            var projectileObject = projectileSpawner.SpawnInstance();
            var projectile = projectileObject.GetComponent<ProjectileBase>();
            if (projectile != null) {
                projectile.Direction = facing.DirVector;
            }

            var skullProjectile = projectileObject.GetComponent<SkullflameController>();
            if (skullProjectile != null) {
                skullProjectile.SetFacingDir(facing.Dir);
                skullProjectile.LifeTime = projectileLifetime;
            }

            var flyProjectile = projectileObject.GetComponent<GiantFlyController>();
            if (flyProjectile != null) {
                flyProjectile.SetFacingDir(facing.Dir);
            }
        }

        public void OnAfterHit() {
            if (!damageable.IsDead) {
                anim.SetTrigger(SimpleShooterAnimKeys.Hit);
            }
        }

        public void OnDeath() {
            anim.SetTrigger(SimpleShooterAnimKeys.Dead);
        }

        public void DestroyAndSpawnDebris() {
            Debug.Log("Destroyed");
            GetComponent<SpawnComponent>().Spawn();
            Destroy(gameObject);
        }
    }
}
