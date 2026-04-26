using Game.Core.Components.Animation;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.SpectralSwords {
    /// <summary>
    /// Per-sword runtime. Telegraphs for <c>telegraphTime</c> in place, then flies along
    /// <c>flightDirection</c> at <c>flightSpeed</c>, ignoring level geometry. At end of life
    /// (max travel distance or lifetime) it switches the sprite animator to the Destroy clip
    /// and despawns once that clip finishes. Damage is delivered by a Damager + trigger
    /// Collider2D placed on a child GameObject (e.g. SwordHitbox) so the hitbox can be
    /// rotated independently of the sword root.
    /// </summary>
    public class SpectralSword : MonoBehaviour {
        [Tooltip("Sprite animator that drives the sword's visuals. Auto-resolved via GetComponentInChildren if not set.")]
        [SerializeField]
        private MultiStateSpriteAnimator spriteAnimator;

        [Tooltip("Clip on the animator to play at end of life. Sword despawns once the clip's OnComplete fires.")]
        [SerializeField]
        private string destroyClipName = "Destroy";

        private Vector2 flightDirection;
        private float flightSpeed;
        private float telegraphTime;
        private float maxTravelDistance;
        private float lifetime;

        private float elapsed;
        private float travelled;
        private bool isFlying;
        private bool isDestroying;

        public void Configure(Vector2 direction, float speed, float telegraph, float maxDistance, float life) {
            flightDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.down;
            flightSpeed = speed;
            telegraphTime = telegraph;
            maxTravelDistance = maxDistance;
            lifetime = life;
        }

        private void Awake() {
            if (spriteAnimator == null) {
                spriteAnimator = GetComponentInChildren<MultiStateSpriteAnimator>(true);
            }
        }

        private void Update() {
            elapsed += Time.deltaTime;

            if (isDestroying) {
                return;
            }

            if (!isFlying) {
                if (elapsed >= telegraphTime) {
                    isFlying = true;
                }
                return;
            }

            Vector2 step = flightDirection * (flightSpeed * Time.deltaTime);
            transform.position += (Vector3)step;
            travelled += step.magnitude;

            if (travelled >= maxTravelDistance || elapsed >= telegraphTime + lifetime) {
                BeginDestroyAnimation();
            }
        }

        private void BeginDestroyAnimation() {
            isDestroying = true;

            // Defensive fallback: if the animator or destroy clip are missing/misconfigured,
            // skip the visual and despawn immediately so the sword can't linger forever.
            if (spriteAnimator == null || string.IsNullOrEmpty(destroyClipName)) {
                Destroy(gameObject);
                return;
            }

            spriteAnimator.SetClip(destroyClipName);
            StateAnimationClip clip = spriteAnimator.CurrentClip;
            if (clip == null || clip.Name != destroyClipName || clip.Loop) {
                Destroy(gameObject);
                return;
            }

            clip.OnComplete.AddListener(OnDestroyClipFinished);
        }

        private void OnDestroyClipFinished() {
            Destroy(gameObject);
        }
    }
}
