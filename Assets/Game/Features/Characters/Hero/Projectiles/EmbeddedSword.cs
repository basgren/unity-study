using Game.Core.Components.GameObjects;
using UnityEngine;

namespace Game.Features.Characters.Hero.Projectiles {
    public class EmbeddedSword : MonoBehaviour {
        private GameObject linkedObject;
        private bool destroyWithLinked;

        // Update is called once per frame
        void Update() {
            if (destroyWithLinked && linkedObject == null) {
                GetComponent<LootDropper>().DropLoot();
                Destroy(gameObject);
            }
        }

        public void LinkWith(GameObject other) {
            if (other != null) {
                linkedObject = other;
                destroyWithLinked = true;
                transform.parent = other.transform;
            }
        }
    }
}
