using System;
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
                instance = Instantiate(effect, transform.position, Quaternion.identity);
            }
        }

        private void OnDestroy() {
            if (instance != null) {
                instance.Stop();
            }
        }
    }
}
