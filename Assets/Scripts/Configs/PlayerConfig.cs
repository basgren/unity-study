using UnityEngine;

namespace Configs {
    [CreateAssetMenu(menuName = "Game/Player Config", fileName = "PlayerConfig")]
    public class PlayerConfig : ScriptableObject {
        /// <summary>
        /// Base player health. May be increased due to buffs/debuffs or other stats along the game.
        /// </summary>
        [Min(1)]
        public float baseMaxHealth = 5f;
    }
}
