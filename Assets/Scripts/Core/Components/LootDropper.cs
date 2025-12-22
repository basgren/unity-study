using System;
using Core.Services;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Core.Components {
    public class LootDropper : MonoBehaviour {
        [SerializeField]
        private GameObject lootPrefab;

        [SerializeField]
        private float initialSpeed = 5f;

        [Range(0f, 1f)]
        [SerializeField]
        private float speedRandomFactor;

        [SerializeField]
        private bool randomDirection;

        [SerializeField]
        private int count = 1;

        private readonly float angleSpread = 90f;

        public void DropLoot(int lootCount = 0) {
            if (lootCount == 0) {
                lootCount = count;
            }
            
            for (int i = lootCount; i > 0; i--) {
                var instance = G.Spawner.SpawnCollectible(lootPrefab, transform.position);

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
