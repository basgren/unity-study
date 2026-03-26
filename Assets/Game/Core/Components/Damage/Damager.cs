using System;
using System.Collections.Generic;
using Core.Components.Extensions;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Core.Components.Damage {
    public enum DamagerType {
        Continuous,

        /// <summary>
        /// When damager is active, it will perform a single hit per each Damageable it collides with.
        /// Staying in collision won't damage. To make it possible to hit again, the component should be
        /// disabled and enabled again. This is useful, for example, for attacks with a sword, when one
        /// swing should deal damage just once, even if animation is long enough to stay in collision.  
        /// </summary>
        SingleHit,
        RespawnOnContact // Hit and respawned in the latest safe position.
    }

    public struct HitInfo {
        public readonly Damageable Target;
        public readonly Damager Damager;
        public readonly bool IsDamaged;

        public HitInfo(Damageable target, Damager damager, bool isDamaged) {
            Target = target;
            Damager = damager;
            IsDamaged = isDamaged;
        }
    }
    
    [Serializable]
    public class HitEvent : UnityEvent<HitInfo> {}

    [RequireComponent(typeof(Collider2D))]
    public class Damager : MonoBehaviour {
        [Header("Damage")]
        [SerializeField, Tooltip("Amount of damage dealt to a valid target on contact.")]
        private int damage = 1;

        [SerializeField, Tooltip("Layers that can be damaged by this dealer.")]
        private LayerMask targetLayers;

        [SerializeField]
        private DamagerType type = DamagerType.Continuous;
        
        [SerializeField]
        private HitEvent onHit;
        
        [Header("Destruction")]
        
        [SerializeField]
        private bool destroyOnContact;

        [SerializeField]
        private UnityEvent onDestroy;

        public int Damage => damage;
        public DamagerType Type => type;
        public Collider2D DamageCollider { get; private set; }

        private readonly HashSet<Damageable> damagedObjects = new HashSet<Damageable>();

        private void Awake() {
            DamageCollider = GetComponent<Collider2D>();
        }

        private void OnEnable() {
            damagedObjects.Clear();
        }

        private void Reset() {
            DamageCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other) {
            TryDamage(other);
        }
        
        private void OnTriggerStay2D(Collider2D other) {
            if (type == DamagerType.Continuous) {
                TryDamage(other);
            }
        }

        private void TryDamage(Collider2D other) {
            if (!targetLayers.Contains(other.gameObject)) {
                return;
            }

            if (!other.TryGetComponent<Damageable>(out var damageable)) {
                return;
            }

            if (type == DamagerType.SingleHit && damagedObjects.Contains(damageable)) {
                return;
            }

            bool isDamaged = damageable.TryTakeDamage(this);

            onHit?.Invoke(new HitInfo(damageable, this, isDamaged));

            if (type == DamagerType.SingleHit && isDamaged) {
                damagedObjects.Add(damageable);
                if (destroyOnContact) {
                    Destroy(gameObject);
                    onDestroy?.Invoke();
                }
            }
        }

        private void OnValidate() {
            if (damage <= 0) {
                Debug.LogWarning($"[DamageDealer] Damage is zero or negative on '{name}'. It will not hurt anything.",
                    this);
            }

            if (targetLayers == 0) {
                Debug.LogWarning($"[DamageDealer] TargetLayers is empty on '{name}'. It will never hit any target.",
                    this);
            }
        }
    }
}
