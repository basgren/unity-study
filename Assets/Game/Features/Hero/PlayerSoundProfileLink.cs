using UnityEngine;

namespace Game.Features.Characters.Hero {
    public class PlayerSoundProfileLink: MonoBehaviour {
        [SerializeField]
        private PlayerSoundProfile profile;

        public PlayerSoundProfile Profile => profile;
    }
}
