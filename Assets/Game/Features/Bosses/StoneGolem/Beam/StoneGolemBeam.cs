using System;
using Game.Core.Components.Animation;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem.Beam {
    /// <summary>
    /// Self-driven beam: the multi-state sprite animator plays start → loop → finish. The beam
    /// length is rebuilt every FixedUpdate from a forward raycast — when ground is hit, the
    /// sprite + damage collider clip to the hit distance. Length tracking runs during ALL phases
    /// so buildup ("start") and fade ("finish") visuals also fit the environment; only during
    /// "loop" is the damage collider enabled and the impact effect shown. The "finish" clip's
    /// last frame fires <see cref="OnBeamFinish"/> as an animation event, raising
    /// <see cref="Finished"/> so the spawning action can despawn the beam.
    ///
    /// Loop duration is set by <see cref="Initialize"/> (typically from the spawning action's
    /// MaxDuration). The serialized <see cref="duration"/> is a fallback for when the beam is
    /// dropped into a scene without an action driving it.
    ///
    /// Pivot-drift compensation: the SpriteRenderer + damage collider live on a child
    /// <see cref="body"/> GameObject whose localPosition is shifted every resize so that the
    /// sprite's pivot pixel stays at this root's origin (= the muzzle). Beam direction is this
    /// transform's local +X; facing flips via the parent hierarchy's <c>localScale.x</c>.
    ///
    /// Aim sweep: the beam does not track the player. It fires in the golem's facing direction and,
    /// once the "loop" (damage) phase begins, rotates about the muzzle from <see cref="startAngleDeg"/>
    /// up by <see cref="sweepArcDeg"/> over <see cref="sweepTime"/>, then holds. The player runs out
    /// from under the rising beam and can close in once it has swept up.
    /// </summary>
    public class StoneGolemBeam : MonoBehaviour {
        [Header("Body wiring")]
        [SerializeField]
        [Tooltip("Child GameObject holding the SpriteRenderer / MultiStateSpriteAnimator / damage collider.")]
        private Transform body;

        [SerializeField]
        [Tooltip("SpriteRenderer on body. Must use Draw Mode = Tiled.")]
        private SpriteRenderer spriteRenderer;

        [SerializeField]
        [Tooltip("MultiStateSpriteAnimator on body driving the start / loop / finish clips.")]
        private MultiStateSpriteAnimator anim;

        [SerializeField]
        [Tooltip("BoxCollider2D on body. Resized at runtime to match the clipped beam length.")]
        private BoxCollider2D damageCollider;

        [Header("Loop timing")]
        [SerializeField]
        [Tooltip("Fallback loop duration if Initialize is not called. StoneGolemBeamShootAction overrides this with the action's MaxDuration.")]
        private float duration = 3f;

        [Header("Ground detection")]
        [SerializeField]
        [Tooltip("When disabled, the beam ignores ground entirely: it always extends to Max Range and " +
                 "passes through the environment (no clipping, no impact effect). More fun to run from.")]
        private bool collideWithGround = true;

        [SerializeField]
        [Tooltip("Layers treated as ground for length-clipping raycasts.")]
        private LayerMask groundLayerMask;

        [SerializeField]
        [Tooltip("Beam extends this far when no ground is hit.")]
        private float maxRange = 20f;

        [Header("Aim sweep")]
        [SerializeField]
        [Tooltip("Local Z angle (degrees) the beam starts at, relative to the golem's facing. " +
                 "Negative points below horizontal. Default -45 = 45° down.")]
        private float startAngleDeg = -45f;

        [SerializeField]
        [Tooltip("Degrees the beam rotates UP from the start angle across the loop (damage) phase. " +
                 "Default 90 ends 45° above horizontal.")]
        private float sweepArcDeg = 90f;

        [SerializeField]
        [Tooltip("Seconds to sweep the full arc once the loop phase begins. Should be <= the loop " +
                 "duration; any remaining loop time holds the beam at the final angle.")]
        private float sweepTime = 2f;

        [Header("Impact")]
        [SerializeField]
        [Tooltip("Spawned at the ground hit point only while the damage collider is active (loop phase). " +
                 "Use a ParticleSystem with Stop Action = Destroy for graceful finish on beam end.")]
        private GameObject impactEffectPrefab;

        /// <summary>Raised once when the "finish" clip reaches its end frame.</summary>
        public event Action Finished;

        private float timer;
        private GameObject impactInstance;
        private bool sweeping;
        private float sweepElapsed;
        private bool externalAim;

        /// <summary>
        /// Sets the loop duration and runs an initial resize — call right after Instantiate.
        /// A non-positive <paramref name="loopDuration"/> is ignored (keeps the serialized
        /// fallback) so passing the action's safety-disabled <c>MaxDuration=0</c> doesn't
        /// strand the beam with a zero-length loop it can't exit.
        /// </summary>
        public void Initialize(float loopDuration) {
            if (loopDuration > 0f) {
                duration = loopDuration;
            }

            // Point at the start angle before the first render so the buildup ("start") clip already
            // shows where the sweep begins, and so the initial resize raycasts in the right direction.
            ApplyAimAngle(startAngleDeg);

            // Initial size before the first render so the spawn frame doesn't flash at prefab default.
            UpdateBeamLength();
        }

        /// <summary>
        /// Switches the beam to externally-driven aim: disables the self-sweep and pins the local aim to
        /// <paramref name="localAngleDeg"/>. The Laser Cross uses this to parent 4 beams under a shared
        /// pivot at fixed offsets and rotate the pivot itself, instead of each beam sweeping on its own.
        /// </summary>
        public void SetManagedAim(float localAngleDeg) {
            externalAim = true;
            startAngleDeg = localAngleDeg;
            sweeping = false;
            ApplyAimAngle(localAngleDeg);
        }

        private void Reset() {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (spriteRenderer != null) {
                body = spriteRenderer.transform;
            }

            anim = GetComponentInChildren<MultiStateSpriteAnimator>(true);
            damageCollider = GetComponentInChildren<BoxCollider2D>(true);
        }

        private void Awake() {
            if (damageCollider != null) {
                damageCollider.enabled = false;
            }

            ApplyAimAngle(startAngleDeg);
        }

        private void FixedUpdate() {
            // Advance the aim before sizing so the raycast/length match the new direction this frame.
            AdvanceSweep();
            UpdateBeamLength();

            if (timer > 0) {
                timer -= Time.fixedDeltaTime;

                if (timer <= 0) {
                    damageCollider.enabled = false;
                    timer = 0f;
                    anim.SetClip("finish");
                    ReleaseImpact();
                }
            }
        }

        public void OnAnimFrame(MultiStateSpriteAnimator animator) {
            // First frame of "loop" arms the damager, starts the loop-duration countdown, and begins
            // the aim sweep. Sizing is handled in FixedUpdate (runs continuously across all clips).
            if (!damageCollider.enabled && animator.CurrentClip.Name == "loop" && animator.CurrentFrameIndex == 0) {
                damageCollider.enabled = true;
                timer = duration;
                // Externally-aimed beams (Laser Cross) are rotated by their pivot, not their own sweep.
                if (!externalAim) {
                    sweeping = true;
                    sweepElapsed = 0f;
                }
            }
        }

        // Rotates the beam from startAngleDeg up by sweepArcDeg over sweepTime, then holds. The facing
        // flip is a parent localScale.x mirror, which preserves the vertical component, so the same
        // local angle reads as "below"/"above" horizontal whether the golem faces left or right.
        private void AdvanceSweep() {
            if (!sweeping) {
                return;
            }

            sweepElapsed += Time.fixedDeltaTime;
            float t = sweepTime > 0f ? Mathf.Clamp01(sweepElapsed / sweepTime) : 1f;
            ApplyAimAngle(startAngleDeg + sweepArcDeg * t);
            if (t >= 1f) {
                sweeping = false;
            }
        }

        private void ApplyAimAngle(float angleDeg) {
            transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
        }

        /// <summary>Animation event on the last frame of the "finish" clip.</summary>
        public void OnBeamFinish() {
            Finished?.Invoke();
        }

        private void OnDestroy() {
            // If the beam was destroyed mid-life (e.g. golem death cancelled the action), still
            // detach the impact so any particle Stop Action can play out.
            ReleaseImpact();
        }

        private void UpdateBeamLength() {
            // Read metrics from the *current* sprite each call: clips may share a sprite size,
            // but reading from spriteRenderer.sprite directly avoids stale Awake-cached values.
            Sprite s = spriteRenderer != null ? spriteRenderer.sprite : null;
            if (s == null) {
                return;
            }

            float nativeWidth = s.rect.width / s.pixelsPerUnit;
            float pivotFractionX = s.pivot.x / s.rect.width;

            float length;
            Vector2? hitPoint;
            if (collideWithGround) {
                Vector2 origin = transform.position;
                // Use the full local→world transform, not transform.right: the golem's facing flip is a
                // parent localScale.x = -1 mirror, which transform.right (rotation-only) does not reflect,
                // so a left-facing beam would otherwise raycast to the right. TransformVector includes it.
                Vector2 direction = ((Vector2)transform.TransformVector(Vector3.right)).normalized;

                RaycastHit2D hit = Physics2D.Raycast(origin, direction, maxRange, groundLayerMask);
                if (hit.collider != null) {
                    length = hit.distance;
                    hitPoint = hit.point;
                } else {
                    length = maxRange;
                    hitPoint = null;
                }
            } else {
                // Pass-through mode: no raycast, beam stays at full length with no contact point.
                length = maxRange;
                hitPoint = null;
            }

            SetBeamLength(length, nativeWidth, pivotFractionX);

            // Impact only while the damage collider is active (loop phase). During start/finish
            // the beam visual fits the ground but no contact effect is spawned.
            if (damageCollider.enabled) {
                UpdateImpact(hitPoint);
            } else {
                ReleaseImpact();
            }
        }

        // Length L = world distance from muzzle (this.transform.position) to beam tip.
        // - renderedSize  = L + pivotFrac.x * nativeWidth  (extra keeps the "behind muzzle" cap visible)
        // - body.localX   = pivotFrac.x * (renderedSize - nativeWidth)  (cancels Unity's tiled-mode pivot drift)
        // - collider size = L, centered between muzzle (0) and tip (L), so pre-muzzle visual is non-damaging.
        private void SetBeamLength(float length, float nativeWidth, float pivotFractionX) {
            float renderedSize = length + pivotFractionX * nativeWidth;

            Vector2 sprSize = spriteRenderer.size;
            sprSize.x = renderedSize;
            spriteRenderer.size = sprSize;

            Vector3 bodyLocal = body.localPosition;
            bodyLocal.x = pivotFractionX * (renderedSize - nativeWidth);
            body.localPosition = bodyLocal;

            Vector2 colSize = damageCollider.size;
            colSize.x = length;
            damageCollider.size = colSize;

            // Collider center is at L/2 in muzzle-space; convert to body-local by subtracting body's offset.
            Vector2 colOffset = damageCollider.offset;
            colOffset.x = length / 2f - bodyLocal.x;
            damageCollider.offset = colOffset;
        }

        private void UpdateImpact(Vector2? hitPoint) {
            if (hitPoint == null) {
                ReleaseImpact();
                return;
            }

            if (impactInstance == null && impactEffectPrefab != null) {
                // World space (no parent) so the beam's facing scale-flip doesn't mess with particles.
                impactInstance = Instantiate(impactEffectPrefab);
            }

            if (impactInstance != null) {
                impactInstance.transform.position = hitPoint.Value;
            }
        }

        // Detach so the impact survives this beam's destruction; if it carries a ParticleSystem,
        // stop emission and let particles already alive play out (the prefab should set
        // Stop Action = Destroy so the GameObject self-cleans). Otherwise destroy immediately.
        private void ReleaseImpact() {
            if (impactInstance == null) {
                return;
            }

            ParticleSystem ps = impactInstance.GetComponentInChildren<ParticleSystem>();
            if (ps != null) {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            } else {
                Destroy(impactInstance);
            }

            impactInstance = null;
        }
    }
}
