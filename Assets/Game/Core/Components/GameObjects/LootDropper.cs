using System;
using System.Collections.Generic;
using Game.Core.Bootstrap;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Core.Components.GameObjects {
    /// <summary>
    /// Single designer-defined loot entry: which prefab to spawn and how many copies.
    /// </summary>
    [Serializable]
    public class LootEntry {
        public GameObject prefab;
        public int count = 1;
    }

    /// <summary>
    /// Spawns configured loot items with a physics scatter. Attach to any loot source
    /// (chest, barrel, enemy) and define its contents in the <c>loot</c> list.
    /// </summary>
    public class LootDropper : MonoBehaviour {
        [SerializeField]
        private List<LootEntry> loot = new List<LootEntry>();

        [SerializeField]
        private float initialSpeed = 5f;

        [Range(0f, 1f)]
        [SerializeField]
        private float speedRandomFactor;

        [SerializeField]
        private bool randomDirection;

        private readonly float angleSpread = 90f;

        /// <summary>
        /// Drops the full configured loot list (each entry spawns its prefab <c>count</c> times).
        /// </summary>
        public void DropLoot() {
            if (IsRestoring()) {
                return;
            }

            foreach (var entry in loot) {
                SpawnItems(entry.prefab, entry.count);
            }
        }

        /// <summary>
        /// Drops a runtime-defined amount of the first configured loot prefab
        /// (e.g. coins lost on player death). Entry counts in the list are ignored.
        /// </summary>
        public void DropLoot(int lootCount) {
            if (IsRestoring()) {
                return;
            }

            if (loot.Count == 0) {
                throw new Exception("LootDropper has no loot entries configured");
            }

            SpawnItems(loot[0].prefab, lootCount);
        }

        private static bool IsRestoring() {
            return G.SceneState != null && G.SceneState.IsRestoring;
        }

        private void SpawnItems(GameObject prefab, int count) {
            for (int i = count; i > 0; i--) {
                var instance = G.Spawner.SpawnCollectible(prefab, transform.position);

                var rigidBody = instance.GetComponent<Rigidbody2D>();

                if (rigidBody == null) {
                    throw new Exception("Loot prefab must have a Rigidbody2D component");
                }

                Vector2 dir = Vector2.up;

                if (randomDirection) {
                    float angle = Random.Range(-angleSpread * 0.5f, angleSpread * 0.5f);
                    dir = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
                }

                float speed = initialSpeed;

                if (speedRandomFactor > 0f) {
                    speed *= (1f + Random.Range(-speedRandomFactor, speedRandomFactor));
                }

                rigidBody.velocity = dir * speed;
            }
        }
    }
}
