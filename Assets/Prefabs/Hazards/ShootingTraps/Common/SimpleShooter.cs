using Core.Audio;
using Core.Components.Damage;
using Core.Components.GameObjects;
using Core.Services;
using Core.Utils;
using Prefabs.Characters.Common;
using Prefabs.Hazards.ShootingTraps.Totem.Projectiles;
using UnityEngine;

namespace Prefabs.Hazards.ShootingTraps.Common {
    internal abstract class SimpleShooterAnimKeys {
        public static readonly int Fire = Animator.StringToHash("onFire");
        public static readonly int Hit = Animator.StringToHash("onHit");
        public static readonly int Dead = Animator.StringToHash("onDead");
    }

    [RequireComponent(typeof(Damageable))]
    public class SimpleShooter : MonoBehaviour {
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
            anim.SetTrigger(SimpleShooterAnimKeys.Fire);

            if (shotSound != null) {
                G.Audio.PlayAt(shotSound, transform.position);
            }
        }

        public void SpawnProjectile() {
            var projectileObject = projectileSpawner.SpawnInstance();
            var projectile = projectileObject.GetComponent<ProjectileBase>();
            if (projectile != null) {
                projectile.Direction = new Vector2(-transform.lossyScale.x, 0);
            }

            var skullProjectile = projectileObject.GetComponent<SkullflameController>();
            if (skullProjectile != null) {
                skullProjectile.SetHorzDirection(
                    // reverted, as sprites look left, while we expect them to look right.
                    transform.lossyScale.x < 0 ? HorzDirection2D.Right : HorzDirection2D.Left
                );
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
