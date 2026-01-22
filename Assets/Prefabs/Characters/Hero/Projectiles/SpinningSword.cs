using Core.Components.Extensions;
using Core.Components.GameObjects;
using UnityEngine;

namespace Prefabs.Characters.Hero.Projectiles {
    public class SpinningSword : MonoBehaviour {
        [SerializeField]
        private LayerMask layers = ~0;

        private SpawnComponent embeddedSwordSpawner;


        void Awake() {
            embeddedSwordSpawner = GetComponentInChildren<SpawnComponent>();
        }

        private void OnTriggerEnter2D(Collider2D other) {
            Debug.Log("collision: " + other.gameObject.name);
            if (!layers.Contains(other.gameObject) || other.CompareTag("IgnoresProjectile")) {
                return;
            }
            
            var embedded = embeddedSwordSpawner.SpawnInstance();
            embedded.GetComponent<EmbeddedSword>().LinkWith(other.gameObject);
            // TODO: [BG] Implement destruction of barrel and releasing sword.
            Destroy(gameObject);
        }
    }
}
