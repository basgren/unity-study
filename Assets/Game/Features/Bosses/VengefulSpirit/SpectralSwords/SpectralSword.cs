using Core.Components.Base2D;
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
    /// Movement runs through a kinematic Rigidbody2D + MovePosition so trigger contacts
    /// fire reliably even against stationary targets (e.g. a player hanging on the
    /// grappling hook).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class SpectralSword : MonoBehaviour {
        [Tooltip("Sprite animator that drives the sword's visuals. Auto-resolved via GetComponentInChildren if not set.")]
        [SerializeField]
        private MultiStateSpriteAnimator spriteAnimator;

        [Tooltip("Clip on the animator to play at end of life. Sword despawns once the clip's OnComplete fires.")]
        [SerializeField]
        private string destroyClipName = "Destroy";
        
        [SerializeField]
        private Collider2D hitbox;

        private Vector2 flightDirection;
        private float flightSpeed;
        private float telegraphTime;
        private float maxTravelDistance;
        private float lifetime;

        private float elapsed;
        private float travelled;
        private bool isFlying;
        private bool isDestroying;

        private Rigidbody2D myRigidbody;

        public void Configure(Vector2 direction, float speed, float telegraph, float maxDistance, float life) {
            flightDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.down;
            flightSpeed = speed;
            telegraphTime = telegraph;
            maxTravelDistance = maxDistance;
            lifetime = life;

            GetComponent<Facing2D>().SetByX(flightDirection.x);
        }

        private void Awake() {
            myRigidbody = GetComponent<Rigidbody2D>();
            if (spriteAnimator == null) {
                spriteAnimator = GetComponentInChildren<MultiStateSpriteAnimator>(true);
            }
        }

        private void FixedUpdate() {
            elapsed += Time.fixedDeltaTime;

            if (isDestroying) {
                return;
            }

            if (!isFlying) {
                if (elapsed >= telegraphTime) {
                    isFlying = true;
                }
                return;
            }

            Vector2 step = flightDirection * (flightSpeed * Time.fixedDeltaTime);
            myRigidbody.MovePosition(myRigidbody.position + step);
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
            hitbox.enabled = false;

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
