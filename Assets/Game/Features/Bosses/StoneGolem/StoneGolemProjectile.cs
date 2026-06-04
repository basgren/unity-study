using System;
using System.Collections.Generic;
using Core.Audio;
using Core.Components.Base2D;
using Game.Core.Bootstrap;
using Game.Core.Services.Scene;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem {
    /// <summary>
    /// The golem's "hand" projectile. <see cref="Launch"/> always fires it horizontally in the
    /// golem's facing; for the first <see cref="homingTime"/> seconds it steers onto the live
    /// player position, then locks its direction and flies straight so the attack can be dodged.
    /// Outgoing speed ramps from zero over <see cref="speedBuildUpTime"/>. The hand flies until its
    /// body collider sweeps into ground/wall (cast against <see cref="groundLayerMask"/>) or travels
    /// <see cref="maxRange"/>, embeds for <see cref="returnDelay"/> seconds, then retraces its
    /// outgoing trajectory in reverse and homes onto the live launch socket for the final stretch,
    /// raising <see cref="Returned"/> on arrival. The sprite rotates along the
    /// velocity vector on both legs, but is guaranteed horizontal at launch and straightens back
    /// to horizontal over the last <see cref="straightenTime"/> seconds of the return, so it
    /// leaves and reattaches to the shoulder in the same flat pose. It damages on both legs (its
    /// Damager stays active); only while embedded is it stationary. Impact feedback (sound +
    /// camera shake) and the pre-return warning rumble live here too. The launching action owns
    /// the despawn — the projectile never destroys itself.
    ///
    /// Expects a kinematic <see cref="Rigidbody2D"/> (movement is applied via MovePosition).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class StoneGolemProjectile : MonoBehaviour {
        private enum Phase {
            Idle,
            Outgoing,
            Embedded,
            Returning
        }

        [Header("Flight")]
        [SerializeField]
        [Tooltip("Full travel speed on the outgoing leg.")]
        private float speed = 15f;

        [SerializeField]
        [Tooltip("Seconds to ramp the outgoing speed from 0 up to full Speed. 0 = instant.")]
        private float speedBuildUpTime = 0.3f;

        [SerializeField]
        [Tooltip("Seconds after launch during which the hand steers onto the player: it starts " +
                 "horizontal and points straight at the player by the end of this window, then " +
                 "locks its direction so the attack can be dodged. 0 = pure horizontal shot.")]
        private float homingTime = 0.5f;

        [SerializeField]
        [Tooltip("Travel speed on the return leg.")]
        private float returnSpeed = 15f;

        [SerializeField]
        [Tooltip("Maximum outgoing distance if no ground/wall is hit first. Then it embeds and returns.")]
        private float maxRange = 12f;

        [SerializeField]
        [Tooltip("Seconds the hand stays embedded after impact before flying back. " +
                 "The golem is idle and vulnerable this whole time.")]
        private float returnDelay = 1.5f;

        [Header("Orientation")]
        [SerializeField]
        [Tooltip("Seconds before reaching the socket over which the returning hand rotates back " +
                 "to horizontal, so it reattaches to the shoulder in the same pose it launched in.")]
        private float straightenTime = 0.25f;

        [Header("Collision")]
        [SerializeField]
        [Tooltip("Layers treated as ground/wall that stop the outgoing hand.")]
        private LayerMask groundLayerMask;

        [Header("Feedback")]
        [SerializeField]
        [Tooltip("Played at the socket when the hand is shot. None = silent.")]
        private AudioCue shotSound;

        [SerializeField]
        [Tooltip("Played at the impact point when the hand slams into ground/wall. None = silent.")]
        private AudioCue groundHitSound;

        [SerializeField]
        [Tooltip("Played when the hand pulls out of the ground to fly back. None = silent.")]
        private AudioCue pullOutSound;

        [SerializeField]
        [Tooltip("Camera shake fired when the hand slams into ground/wall.")]
        private CameraShakeSettings groundHitShake = CameraShakeSettings.Default;

        [SerializeField]
        [Tooltip("Smaller warning shake while the hand is about to pull out of the ground. It is " +
                 "timed to END exactly when the return leg begins, so its Duration doubles as the " +
                 "lead time (default: 0.5 s before the return).")]
        private CameraShakeSettings preReturnShake = new CameraShakeSettings {
            amplitude = 0.7f,
            frequency = 3f,
            duration = 0.5f,
            decayDuration = 0.1f
        };

        /// <summary>Raised once when the hand has returned to its launch socket.</summary>
        public event Action Returned;

        private Rigidbody2D myRigidbody;
        private Collider2D bodyCollider;
        private Facing2D facing;
        private ContactFilter2D groundFilter;
        // Reused cast buffer — FixedUpdate must not allocate.
        private readonly RaycastHit2D[] castHits = new RaycastHit2D[4];
        // Outgoing positions recorded each physics step; the return leg retraces them in reverse.
        // Pre-sized so a normal flight never grows the list inside FixedUpdate.
        private readonly List<Vector2> flightPath = new List<Vector2>(512);
        private int pathIndex;
        private Transform returnTarget;
        private Transform homingTarget;
        private Vector2 dir;
        private float launchAngle;
        private float flightTime;
        private float traveled;
        private float embedTimer;
        private bool preReturnShakeFired;
        private bool embeddedInGround;
        private Phase phase = Phase.Idle;

        private void Awake() {
            myRigidbody = GetComponent<Rigidbody2D>();
            // The physical body collider on this root object (the Damager trigger lives on a child).
            bodyCollider = GetComponent<Collider2D>();
            facing = GetComponent<Facing2D>();

            groundFilter.SetLayerMask(groundLayerMask);
            groundFilter.useTriggers = false;
        }

        /// <summary>
        /// Fires the hand horizontally toward <paramref name="dirSign"/> and records the socket it
        /// returns to. For the first <see cref="homingTime"/> seconds the hand steers onto
        /// <paramref name="target"/> (read live each frame); pass null for a plain horizontal shot.
        /// <paramref name="socket"/> is read live each frame on the way back, so the hand still
        /// homes onto the golem even if it was knocked around while the hand was away.
        /// </summary>
        public void Launch(int dirSign, Transform target, Transform socket) {
            returnTarget = socket;
            homingTarget = target;
            dir = new Vector2(dirSign < 0 ? -1f : 1f, 0f);
            launchAngle = dir.x < 0f ? 180f : 0f;
            flightTime = 0f;
            traveled = 0f;
            flightPath.Clear();
            flightPath.Add(myRigidbody.position);
            phase = Phase.Outgoing;
            ApplyOrientation(dir);

            if (shotSound != null) {
                G.Audio.PlayAt(shotSound, myRigidbody.position);
            }
        }

        private void FixedUpdate() {
            switch (phase) {
                case Phase.Outgoing:
                    TickOutgoing();
                    break;
                case Phase.Embedded:
                    TickEmbedded();
                    break;
                case Phase.Returning:
                    TickReturning();
                    break;
            }
        }

        private void TickOutgoing() {
            flightTime += Time.fixedDeltaTime;

            // Steer onto the player during the homing window: blend from the horizontal launch
            // angle to the live to-player angle, so the hand always leaves the shoulder flat and
            // is fully locked on by the end of the window. Afterwards the direction is frozen.
            if (homingTarget != null && homingTime > 0f && flightTime < homingTime) {
                Vector2 toTarget = (Vector2)homingTarget.position - myRigidbody.position;
                if (toTarget.sqrMagnitude > 0.0001f) {
                    float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                    float angle = Mathf.LerpAngle(launchAngle, targetAngle, flightTime / homingTime);
                    dir = AngleToDir(angle);
                }
            }

            ApplyOrientation(dir);

            // Ramp from standstill up to full speed so the throw reads as a heave, not a teleport.
            float currentSpeed = speedBuildUpTime > 0f
                ? speed * Mathf.Clamp01(flightTime / speedBuildUpTime)
                : speed;
            float stepLen = currentSpeed * Time.fixedDeltaTime;

            // Sweep the body collider along this step and stop the moment it touches ground/wall;
            // otherwise advance until maxRange. The collider's offset/size on the prefab defines
            // how deep the hand visually sinks in.
            int hitCount = bodyCollider.Cast(dir, groundFilter, castHits, stepLen);
            if (hitCount > 0) {
                float dist = castHits[0].distance;
                for (int i = 1; i < hitCount; i++) {
                    if (castHits[i].distance < dist) {
                        dist = castHits[i].distance;
                    }
                }

                myRigidbody.MovePosition(myRigidbody.position + dir * Mathf.Max(0f, dist));
                EnterEmbedded(true);
                return;
            }

            traveled += stepLen;
            if (traveled >= maxRange) {
                EnterEmbedded(false);
                return;
            }

            Vector2 newPos = myRigidbody.position + dir * stepLen;
            myRigidbody.MovePosition(newPos);
            flightPath.Add(newPos);
        }

        private void EnterEmbedded(bool hitGround) {
            phase = Phase.Embedded;
            embedTimer = returnDelay;
            preReturnShakeFired = false;
            embeddedInGround = hitGround;

            // Impact feedback only on a real ground/wall hit — a max-range stall in the air
            // stays silent.
            if (hitGround) {
                if (groundHitSound != null) {
                    G.Audio.PlayAt(groundHitSound, myRigidbody.position);
                }

                G.Camera.Shake(groundHitShake);
            }
        }

        private void TickEmbedded() {
            embedTimer -= Time.fixedDeltaTime;

            // Warning rumble, timed so the shake ends right as the hand pulls out (the shake's own
            // duration is the lead time). Ground feedback only — a mid-air max-range stall has
            // nothing to rumble.
            if (!preReturnShakeFired && embeddedInGround && embedTimer <= preReturnShake.duration) {
                preReturnShakeFired = true;
                G.Camera.Shake(preReturnShake);
            }

            if (embedTimer <= 0f) {
                if (embeddedInGround && pullOutSound != null) {
                    G.Audio.PlayAt(pullOutSound, myRigidbody.position);
                }

                // Retrace the outgoing trajectory from its far end.
                pathIndex = flightPath.Count - 1;
                phase = Phase.Returning;
            }
        }

        private void TickReturning() {
            Vector2 socket = returnTarget != null ? (Vector2)returnTarget.position : myRigidbody.position;
            Vector2 pos = myRigidbody.position;
            float budget = returnSpeed * Time.fixedDeltaTime;

            // Retrace the recorded outgoing path backwards, spending this step's travel budget
            // across as many waypoints as it covers (waypoint spacing is one outgoing step, which
            // can be shorter than a return step).
            while (budget > 0f && pathIndex >= 0) {
                Vector2 waypoint = flightPath[pathIndex];
                float dist = (waypoint - pos).magnitude;
                if (dist > budget) {
                    pos += (waypoint - pos) * (budget / dist);
                    budget = 0f;
                    break;
                }

                pos = waypoint;
                budget -= dist;
                pathIndex--;
            }

            // Path exhausted — home onto the live socket for the final stretch, which covers the
            // golem having moved since launch.
            if (budget > 0f) {
                Vector2 toSocket = socket - pos;
                if (toSocket.magnitude <= budget) {
                    myRigidbody.MovePosition(socket);
                    // Arrive in the launch pose: keep the current facing, flat against the shoulder.
                    ApplyOrientation(facing != null ? facing.DirVector : Vector2.right);
                    phase = Phase.Idle;
                    Returned?.Invoke();
                    return;
                }

                pos += toSocket.normalized * budget;
            }

            Vector2 step = pos - myRigidbody.position;
            if (step.sqrMagnitude > 0.000001f) {
                float travelAngle = Mathf.Atan2(step.y, step.x) * Mathf.Rad2Deg;

                // Straighten back to horizontal over the last straightenTime seconds of the flight
                // so the hand reattaches to the shoulder in the same flat pose it launched in.
                float remainingTime = returnSpeed > 0f ? (socket - pos).magnitude / returnSpeed : 0f;
                if (straightenTime > 0f && remainingTime < straightenTime) {
                    float horizontalAngle = step.x < 0f ? 180f : 0f;
                    travelAngle = Mathf.LerpAngle(horizontalAngle, travelAngle, remainingTime / straightenTime);
                }

                ApplyOrientation(AngleToDir(travelAngle));
            }

            myRigidbody.MovePosition(pos);
        }

        /// <summary>
        /// Points the hand along the given world direction: left/right is mirrored via
        /// <see cref="Facing2D"/> (localScale.x flip, as everywhere in the project) and the
        /// residual tilt is applied as Z rotation. With the mirror active the rendered +X is
        /// world -X, so the local angle is computed from the negated direction.
        /// </summary>
        private void ApplyOrientation(Vector2 worldDir) {
            if (worldDir.sqrMagnitude < 0.0001f) {
                return;
            }

            if (facing != null) {
                facing.SetByX(worldDir.x);
            }

            Vector2 local = facing != null && facing.IsLeft ? -worldDir : worldDir;
            myRigidbody.MoveRotation(Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg);
        }

        private static Vector2 AngleToDir(float angleDeg) {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
    }
}
