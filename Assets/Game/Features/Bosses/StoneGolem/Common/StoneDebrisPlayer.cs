using Core.Audio;
using Game.Core.Bootstrap;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem.Common {
    public class StoneDebrisPlayer : MonoBehaviour {
        [SerializeField]
        [Tooltip("Crushing stones sound played once at this object's position.")]
        private AudioCue crushSound;

        private ParticleSystem[] systems;

        private void Awake() {
            systems = GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        }

        public void Play() {
            foreach (ParticleSystem ps in systems) {
                ps.Play();
            }

            if (crushSound != null) {
                G.Audio.PlayAt(crushSound, transform.position);
            }
        }
    }
}
