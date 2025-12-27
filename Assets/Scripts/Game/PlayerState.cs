using Configs;

namespace Game {
    public class PlayerState {
        public float baseMaxHealth;
        public float currentHealth;
        public int coinsValue = 0;
        public bool isArmed = false;

        public PlayerState(PlayerConfig config) {
            baseMaxHealth = config.baseMaxHealth;
            currentHealth = baseMaxHealth;
        }

        /// <summary>
        /// Current max health taking into accounts all buffs and level-ups.
        /// </summary>
        /// <returns></returns>
        public float GetMaxHealth() {
            return baseMaxHealth;
        }
    }
}
