using System;
using Game.Core.Components.Animation;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem.Beam {
    /// <summary>
    /// Passive beam visual driven by its spawning action. The multi-state sprite animator plays
    /// start → loop → finish. The beam length is rebuilt every FixedUpdate from a forward raycast —
    /// when ground is hit, the sprite + damage collider clip to the hit distance. Length tracking
    /// runs during ALL phases so buildup ("start") and fade ("finish") visuals also fit the
    /// environment; only during "loop" is the damage collider enabled and the impact effect shown.
    ///
    /// The beam owns no aim or timing of its own. The action positions and rotates it
    /// (<see cref="SetAim"/>), decides how long the loop lasts, and ends it (<see cref="PlayFinish"/>).
    /// The beam reports back two moments: <see cref="LoopStarted"/> fires on the loop clip's first
    /// frame (when the damage collider arms), letting the action start its duration countdown; the
    /// "finish" clip's last frame fires <see cref="OnBeamFinish"/> as an animation event, raising
    /// <see cref="Finished"/> so the action can despawn the beam.
    ///
    /// Pivot-drift compensation: the SpriteRenderer + damage collider live on a child
    /// <see cref="body"/> GameObject whose localPosition is shifted every resize so that the
    /// sprite's pivot pixel stays at this root's origin (= the muzzle). Beam direction is this
    /// transform's local +X; facing flips via the parent hierarchy's <c>localScale.x</c>.
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

        [Header("Impact")]
        [SerializeField]
        [Tooltip("Spawned at the ground hit point only while the damage collider is active (loop phase). " +
                 "Use a ParticleSystem with Stop Action = Destroy for graceful finish on beam end.")]
        private GameObject impactEffectPrefab;

        /// <summary>
        /// Raised on the loop clip's first frame, the moment the damage collider arms. The driving
        /// action listens to this to start its loop-duration countdown.
        /// </summary>
        public event Action LoopStarted;

        /// <summary>Raised once when the "finish" clip reaches its end frame.</summary>
        public event Action Finished;

        private GameObject impactInstance;
        private bool finishing;

        /// <summary>
        /// Sets the beam's aim — its local Z rotation, in degrees. The beam never changes this on
        /// its own; the driving action positions and sweeps it. Call right after Instantiate so the
        /// buildup ("start") clip and the first length raycast already point the right way.
        /// </summary>
        public void SetAim(float localAngleDeg) {
            transform.localRotation = Quaternion.Euler(0f, 0f, localAngleDeg);
            // Resize to the new direction immediately so the beam never lags a physics frame behind
            // the aim (and the spawn frame doesn't flash at the prefab's default length).
            UpdateBeamLength();
        }

        /// <summary>
        /// Ends the beam: closes the damage window and plays the "finish" clip, whose last frame
        /// raises <see cref="Finished"/>. Called by the driving action when its loop duration
        /// elapses. Idempotent — extra calls are ignored.
        /// </summary>
        public void PlayFinish() {
            if (finishing) {
                return;
            }

            finishing = true;
            if (damageCollider != null) {
                damageCollider.enabled = false;
            }

            if (anim != null) {
                anim.SetClip("finish");
            }

            ReleaseImpact();
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
        }

        private void FixedUpdate() {
            // Keep the beam fitted to the environment every physics step. Aim is owned by the
            // driving action (see SetAim); the beam only sizes itself to its current direction.
            UpdateBeamLength();
        }

        public void OnAnimFrame(MultiStateSpriteAnimator animator) {
            // First frame of "loop" arms the damager and notifies the action so it can start its
            // loop-duration countdown. Sizing is handled in FixedUpdate (runs across all clips).
            if (!damageCollider.enabled && !finishing && animator.CurrentClip.Name == "loop" && animator.CurrentFrameIndex == 0) {
                damageCollider.enabled = true;
                LoopStarted?.Invoke();
            }
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
            Vector2? hitNormal;
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
                    hitNormal = hit.normal;
                } else {
                    length = maxRange;
                    hitPoint = null;
                    hitNormal = null;
                }
            } else {
                // Pass-through mode: no raycast, beam stays at full length with no contact point.
                length = maxRange;
                hitPoint = null;
                hitNormal = null;
            }

            SetBeamLength(length, nativeWidth, pivotFractionX);

            // Impact only while the damage collider is active (loop phase). During start/finish
            // the beam visual fits the ground but no contact effect is spawned.
            if (damageCollider.enabled) {
                UpdateImpact(hitPoint, hitNormal);
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

        private void UpdateImpact(Vector2? hitPoint, Vector2? hitNormal) {
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

                // The BeamSpot is an ellipse oriented along a horizontal surface by default. A vertical
                // surface (wall) yields a mostly-horizontal hit normal, so rotate the spot 90° to lie
                // along the wall instead. The effect is symmetric, so either rotation direction reads
                // the same — a binary 0°/90° choice keeps the sprite on a clean pixel grid.
                if (hitNormal != null) {
                    bool verticalSurface = Mathf.Abs(hitNormal.Value.x) > Mathf.Abs(hitNormal.Value.y);
                    impactInstance.transform.rotation = Quaternion.Euler(0f, 0f, verticalSurface ? 90f : 0f);
                }
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
