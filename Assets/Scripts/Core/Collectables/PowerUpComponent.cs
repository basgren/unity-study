using Core.Services;
using UnityEngine;

namespace Core.Collectables
{
    /// <summary>
    /// Provides additional effects to power ups.
    /// </summary>
    public class PowerUpComponent : MonoBehaviour {
        [SerializeField]
        private ParticleSystem effect;

        private ParticleSystem instance;
        
        void Start() {
            if (effect != null) {
                instance = G.Spawner.SpawnVfx(effect, transform.position);
            }
        }

        private void OnDestroy() {
            if (instance != null) {
                instance.Stop();
            }
        }
    }
}
