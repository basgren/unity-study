using Core.Extensions;
using UnityEngine;

namespace Components {
    public enum DamagerType {
        Simple,
        RespawnOnContact // Hit and respawned in the latest safe position.
    }
    
    [RequireComponent(typeof(Collider2D))]
    public class Damager : MonoBehaviour {
        [Header("Damage")]
        [SerializeField, Tooltip("Amount of damage dealt to a valid target on contact.")]
        private int damage = 1;

        [SerializeField, Tooltip("Layers that can be damaged by this dealer.")]
        private LayerMask targetLayers;
        
        [SerializeField]
        private DamagerType type = DamagerType.Simple;

        public int Damage => damage;
        public DamagerType Type => type; 
        public Collider2D DamageCollider { get; private set; }

        private void Awake() {
            DamageCollider = GetComponent<Collider2D>();
        }

        private void Reset() {
            DamageCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other) {
            TryDamage(other);
        }

        private void TryDamage(Collider2D other) {
            if (!targetLayers.Contains(other.gameObject)) {
                return;
            }

            if (!other.TryGetComponent<Damageable>(out var damageable)) {
                return;
            }

            damageable.TryTakeDamage(this);
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
