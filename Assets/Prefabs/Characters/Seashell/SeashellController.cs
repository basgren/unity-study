using Core.Components.GameObjects;
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

        [SerializeField]
        private GameObject attackArea;
        
        private Animator anim;

        private TinyTimer shootCooldownTimer;
        private TinyTimer biteCooldownTimer;

        private void Awake() {
            shootCooldownTimer = new TinyTimer(shootCooldown);
            biteCooldownTimer = new TinyTimer(biteCooldown);
            
            anim = GetComponentInChildren<Animator>();
            
            CloseDamageWindow();
        }

        private void Update() {
            shootCooldownTimer.Update(Time.deltaTime);
            biteCooldownTimer.Update(Time.deltaTime);
        }

        public bool CanShoot() {
            return shootCooldownTimer.IsTimedOut;
        }
        
        public bool CanBite() {
            return biteCooldownTimer.IsTimedOut;
        }
        
        public void Shoot() {
            if (!shootCooldownTimer.IsTimedOut) {
                return;
            }

            shootCooldownTimer.Start();
            anim.SetTrigger(SeashellAnimKeys.Fire);
        }

        public void SpawnProjectile() {
            projectileSpawner.Spawn();
        }
        
        public void Bite() {
            biteCooldownTimer.Start();
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
