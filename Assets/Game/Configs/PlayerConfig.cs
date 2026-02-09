using UnityEngine;

namespace Game.Configs {
    [CreateAssetMenu(menuName = "Config/Player Config", fileName = "PlayerConfig")]
    public class PlayerConfig : ScriptableObject {
        /// <summary>
        /// Base player health. May be increased due to buffs/debuffs or other stats along the game.
        /// </summary>
        [Min(1)]
        public float BaseMaxHealth = 5f;
    }
}
