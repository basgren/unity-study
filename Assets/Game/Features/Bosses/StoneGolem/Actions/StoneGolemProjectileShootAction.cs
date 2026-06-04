using Game.Core.Bootstrap;
using Game.Features.Bosses._Shared;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem.Actions {
    /// <summary>
    /// Launches the golem's hand as a returning projectile. The animator is driven by a single bool
    /// parameter (set true on start, false when the hand returns), so the cast/hold/release flow
    /// lives in the state machine rather than in scattered triggers. On the animation's effect frame
    /// (<see cref="Do"/>) the hand is spawned and launched horizontally in the golem's facing; the
    /// projectile itself steers onto the player during a short homing window, then flies straight.
    /// It travels out, embeds on impact, returns to the socket, then the bool clears, the hand is
    /// despawned, and the action completes.
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

            // The hand always leaves the shoulder horizontally; the projectile steers onto the
            // player during its homing window. Without a player it just flies straight ahead.
            activeProjectile.Launch(golem.FacingSign, ResolvePlayerTransform(), spawnPoint);
        }

        // Live player transform for the projectile's homing window, or null for a plain
        // horizontal shot when the player can't be located.
        private Transform ResolvePlayerTransform() {
            return G.Hero != null && G.Hero.Controller != null
                ? G.Hero.Controller.transform
                : null;
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
