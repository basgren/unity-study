using Game.Core.Bootstrap;
using Game.Features.Bosses._Shared;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem.Actions {
    /// <summary>
    /// Launches the golem's hand as a returning projectile. The animator is driven by a single bool
    /// parameter (set true on start, false when the hand returns), so the cast/hold/release flow
    /// lives in the state machine rather than in scattered triggers. On the animation's effect frame
    /// (<see cref="Do"/>) the hand is spawned and fired toward the player's position captured at that
    /// instant (no homing). It travels out, embeds on impact, returns to the socket, then the bool
    /// clears, the hand is despawned, and the action completes.
    /// </summary>
    public class StoneGolemProjectileShootAction : EnemyAction {
        [Header("References")]
        [SerializeField]
        private StoneGolem golem;

        [SerializeField]
        private Animator animator;

        [SerializeField]
        [Tooltip("Projectile prefab carrying a StoneGolemProjectile (the hand).")]
        private GameObject projectilePrefab;

        [SerializeField]
        private Transform spawnPoint;

        [Header("Animation")]
        [SerializeField]
        [Tooltip("Bool parameter on the golem animator. Set true on action start, false when the projectile returns.")]
        private string animBoolKey = "isShootingHand";

        private int animBoolHash;
        private StoneGolemProjectile activeProjectile;

        private void Awake() {
            animBoolHash = Animator.StringToHash(animBoolKey);
        }

        protected override void OnBegin() {
            if (animator != null) {
                animator.SetBool(animBoolHash, true);
            }
        }

        public override void Do() {
            if (projectilePrefab == null || spawnPoint == null) {
                // Wiring missing — the base safety timeout will still end the action cleanly.
                return;
            }

            GameObject instance = Instantiate(projectilePrefab, spawnPoint.position, Quaternion.identity);
            activeProjectile = instance.GetComponent<StoneGolemProjectile>();
            if (activeProjectile == null) {
                Destroy(instance);
                return;
            }

            activeProjectile.Returned += OnProjectileReturned;

            // Capture the player's position at the moment of launch — the hand flies straight there
            // (no homing). Fall back to straight ahead if the player can't be located.
            activeProjectile.Launch(ResolveTargetPoint(), spawnPoint);
        }

        // The aim point is the player's position sampled now (launch frame); the hand does not track
        // the player afterward. Without a player it shoots one unit ahead in the golem's facing.
        private Vector2 ResolveTargetPoint() {
            Transform player = G.Hero != null && G.Hero.Controller != null
                ? G.Hero.Controller.transform
                : null;
            if (player != null) {
                return player.position;
            }

            int sign = golem != null ? golem.FacingSign : 1;
            return (Vector2)spawnPoint.position + new Vector2(sign, 0f);
        }

        private void OnProjectileReturned() {
            Complete();
        }

        protected override void OnEnd() {
            if (activeProjectile != null) {
                activeProjectile.Returned -= OnProjectileReturned;
                Destroy(activeProjectile.gameObject);
                activeProjectile = null;
            }

            // Safety: clear the bool on every end path (natural complete, cancel-on-death, safety
            // timeout) so the animator can never stay stuck in the cast pose.
            if (animator != null) {
                animator.SetBool(animBoolHash, false);
            }
        }
    }
}
