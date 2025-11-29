using System;
using Core.Services;
using UnityEngine;
using Random = UnityEngine.Random;

public class LootDropper : MonoBehaviour {
    [SerializeField]
    private GameObject lootPrefab;
    
    [SerializeField]
    private float initialSpeed = 5f;

    [SerializeField]
    private bool randomDirection = false;
    
    private readonly float angleSpread = 90f;

    public void DropLoot(int count = 1) {
        for (int i = count; i > 0; i--) {
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
            
            rigidBody.velocity = dir * initialSpeed;
        }
    }
}
