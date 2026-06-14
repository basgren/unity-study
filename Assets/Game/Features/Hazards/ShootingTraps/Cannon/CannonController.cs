using Core.Audio;
using Game.Core.Bootstrap;
using Game.Core.Components.Base2D;
using Game.Core.Components.Damage;
using Game.Core.Components.GameObjects;
using Game.Core.Services.SceneState.Savers;
using Game.Core.Utils;
using Game.Features.Characters._Shared;
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

        [Header("Fuse")]
        [Tooltip("Delay between spotting the player and firing. The fuse effect and sound play during this window.")]
        [SerializeField]
        private float fuseDelay = 2f;

        [Tooltip("Pre-placed fuse particle system (child of the cannon). Disable its Play On Awake.")]
        [SerializeField]
        private ParticleSystem fuseEffect;

        [Header("Sounds")]
        [SerializeField]
        private AudioCue shotSound;

        [SerializeField]
        private AudioCue fuseSound;

        private Animator anim;

        private TinyTimer shootCooldownTimer;
        private TinyTimer fuseTimer;
        private bool isFusing;
        private IAudioLoopHandle fuseSoundHandle;
        private Damageable damageable;
        private Facing2D facing;

        private void Awake() {
            shootCooldownTimer = new TinyTimer(shootCooldown);

            fuseTimer = new TinyTimer(fuseDelay);
            fuseTimer.OnTimeout += Fire;

            damageable = GetComponent<Damageable>();
            facing = GetComponent<Facing2D>();

            anim = GetComponentInChildren<Animator>();
        }

        private void Update() {
            shootCooldownTimer.Update(Time.deltaTime);

            if (isFusing) {
                fuseTimer.Update(Time.deltaTime);
            }
        }

        private void OnDestroy() {
            // The looped fuse sound lives on a pooled AudioSource, not under this object,
            // so it must be stopped explicitly if the cannon is destroyed mid-fuse.
            StopFuseSound();
        }

        public bool CanShoot() {
            return shootCooldownTimer.IsTimedOut && !isFusing;
        }

        public void Shoot() {
            if (!CanShoot()) {
                return;
            }

            // Light the fuse: effects play now, the actual shot happens once the fuse burns down.
            isFusing = true;
            fuseTimer.Start();
            StartFuseEffect();
            PlayFuseSound();
        }

        private void Fire() {
            isFusing = false;
            StopFuseEffect();
            StopFuseSound();

            shootCooldownTimer.Start();
            anim.SetTrigger(CannonAnimKeys.Fire);

            if (shotSound != null) {
                G.Audio.PlayAt(shotSound, transform.position);
            }
        }

        private void CancelFuse() {
            if (!isFusing) {
                return;
            }

            isFusing = false;
            fuseTimer.Stop();
            StopFuseEffect();
            StopFuseSound();
        }

        private void StartFuseEffect() {
            if (fuseEffect != null) {
                fuseEffect.Play();
            }
        }

        private void StopFuseEffect() {
            if (fuseEffect != null) {
                // Stop emitting but let already-spawned particles finish their lifetime.
                // The system is reused (not destroyed) and restarted via Play() on the next fuse.
                fuseEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void PlayFuseSound() {
            if (fuseSound != null) {
                fuseSoundHandle = G.Audio.PlayLoopAt(fuseSound, transform.position);
            }
        }

        private void StopFuseSound() {
            if (fuseSoundHandle != null) {
                fuseSoundHandle.Stop();
                fuseSoundHandle = null;
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
            CancelFuse();
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
