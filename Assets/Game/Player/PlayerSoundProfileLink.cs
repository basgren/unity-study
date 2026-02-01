using Game.Audio;
using UnityEngine;

namespace Game.Player {
    public class PlayerSoundProfileLink: MonoBehaviour {
        [SerializeField]
        private PlayerSoundProfile profile;

        public PlayerSoundProfile Profile => profile;
    }
}
