using Core.Audio;
using Core.Components.Extensions;
using Game.Core.Bootstrap;
using Game.Core.Components.Base2D;
using Game.Core.Components.GameObjects;
using Game.Features.Characters._Shared;
using UnityEngine;

namespace Game.Features.Characters.Hero.Projectiles {
    public class SpinningSword : ProjectileBase {
        [SerializeField]
        private LayerMask layers = ~0;
        
        [SerializeField]
        private AudioCue embeddingSound;

        private SpawnComponent embeddedSwordSpawner;
        private Facing2D facing;

        protected override void Awake() {
            base.Awake();
            embeddedSwordSpawner = GetComponentInChildren<SpawnComponent>();
            facing = GetComponent<Facing2D>();
        }

        protected override Vector2 GetNewPosition(Vector2 currentPos) {
            return currentPos + new Vector2(linearSpeed * DeltaTime * facing.DirSign, 0);
        }

        private void OnTriggerEnter2D(Collider2D other) {
            if (!layers.Contains(other.gameObject) || other.CompareTag("IgnoresProjectile")) {
                return;
            }

            if (embeddingSound != null) {
                G.Audio.PlayAt(embeddingSound, transform.position);
            }
            
            var embedded = embeddedSwordSpawner.SpawnInstance();
            embedded.GetComponent<EmbeddedSword>().LinkWith(other.gameObject);
            // TODO: [BG] Implement destruction of barrel and releasing sword.
            Destroy(gameObject);
        }
    }
}
