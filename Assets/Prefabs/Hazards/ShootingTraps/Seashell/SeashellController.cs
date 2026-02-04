using Core.Audio;
using Core.Components.GameObjects;
using Core.Services;
using Core.Utils;
using UnityEngine;

namespace Prefabs.Characters.Seashell {
    internal abstract class SeashellAnimKeys {
        public static readonly int Fire = Animator.StringToHash("onFire");
        public static readonly int Bite = Animator.StringToHash("onBite");
        public static readonly int Hit = Animator.StringToHash("onHit");
        public static readonly int Dead = Animator.StringToHash("onDead");
    }
    
    public class SeashellController : MonoBehaviour {
        [SerializeField]
        private SpawnComponent projectileSpawner;

        [SerializeField]
        private float shootCooldown = 2f;
        
        [SerializeField]
        private float biteCooldown = 3f;
        
        /// <summary>
        /// Common cooldown, so after mob does any kind of attack, this amount of time should pass, otherwise
        /// it will be to agressive. Shooting and biting as soon as shooting in progress. 
        /// </summary>
        [SerializeField]
        private float commonCooldown = 1f;

        [SerializeField]
        private GameObject attackArea;
        
        [Header("Sounds")]
        [SerializeField]
        private AudioCue shotSound; 
        
        private Animator anim;

        private TinyTimer shootCooldownTimer;
        private TinyTimer biteCooldownTimer;
        private TinyTimer commonCooldownTimer;

        private void Awake() {
            shootCooldownTimer = new TinyTimer(shootCooldown);
            biteCooldownTimer = new TinyTimer(biteCooldown);
            commonCooldownTimer = new TinyTimer(commonCooldown);
            
            anim = GetComponentInChildren<Animator>();
            
            CloseDamageWindow();
        }

        private void Update() {
            shootCooldownTimer.Update(Time.deltaTime);
            biteCooldownTimer.Update(Time.deltaTime);
            commonCooldownTimer.Update(Time.deltaTime);
        }

        public bool CanShoot() {
            return shootCooldownTimer.IsTimedOut && commonCooldownTimer.IsTimedOut;
        }
        
        public bool CanBite() {
            return biteCooldownTimer.IsTimedOut && commonCooldownTimer.IsTimedOut;
        }
        
        public void Shoot() {
            if (!CanShoot()) {
                return;
            }

            shootCooldownTimer.Start();
            commonCooldownTimer.Start();
            anim.SetTrigger(SeashellAnimKeys.Fire);

            if (shotSound != null) {
                G.Audio.PlayAt(shotSound, transform.position);
            }
        }

        public void SpawnProjectile() {
            projectileSpawner.Spawn();
        }
        
        public void Bite() {
            if (!CanBite()) {
                return;
            }
            
            biteCooldownTimer.Start();
            commonCooldownTimer.Start();
            anim.SetTrigger(SeashellAnimKeys.Bite);
        }

        public void OpenDamageWindow() {
            attackArea.SetActive(true);            
        }

        public void CloseDamageWindow() {
            attackArea.SetActive(false);
        }

        public void OnAfterHit() {
            CloseDamageWindow();
            anim.SetTrigger(SeashellAnimKeys.Hit);
        }

        public void OnDeath() {
            anim.SetTrigger(SeashellAnimKeys.Dead);
        }

        public void DestroyAndSpawnDebris() {
            GetComponent<SpawnComponent>().Spawn();
            Destroy(gameObject);
        }
    }
}
