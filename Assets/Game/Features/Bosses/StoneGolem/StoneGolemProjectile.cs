using System;
using Core.Components.Base2D;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem {
    /// <summary>
    /// Straight-shot "hand" projectile. The launching action captures the player's position at the
    /// moment of fire and calls <see cref="Launch"/>; the hand flies in that fixed direction (no
    /// homing) until it hits ground/wall (raycast against <see cref="groundLayerMask"/>) or travels
    /// <see cref="maxRange"/>. It then embeds for <see cref="returnDelay"/> seconds and flies back to
    /// the live launch socket, raising <see cref="Returned"/> on arrival. It damages on both the
    /// outgoing and return legs (its Damager stays active); only while embedded is it stationary.
    /// The launching action owns the despawn — the projectile never destroys itself.
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
        [Tooltip("Travel speed on the outgoing leg.")]
        private float speed = 15f;

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

        [Header("Collision")]
        [SerializeField]
        [Tooltip("Layers treated as ground/wall that stop the outgoing hand.")]
        private LayerMask groundLayerMask;

        /// <summary>Raised once when the hand has returned to its launch socket.</summary>
        public event Action Returned;

        private Rigidbody2D myRigidbody;
        private Facing2D facing;
        private Transform returnTarget;
        private Vector2 dir;
        private float traveled;
        private float embedTimer;
        private Phase phase = Phase.Idle;

        private void Awake() {
            myRigidbody = GetComponent<Rigidbody2D>();
            facing = GetComponent<Facing2D>();
        }

        /// <summary>
        /// Fires the hand toward <paramref name="targetPoint"/> (the player's position captured at
        /// launch) and records the socket it returns to. <paramref name="socket"/> is read live each
        /// frame on the way back, so the hand still homes onto the golem even if it was knocked around
        /// while the hand was away.
        /// </summary>
        public void Launch(Vector2 targetPoint, Transform socket) {
            returnTarget = socket;
            Vector2 toTarget = targetPoint - myRigidbody.position;
            dir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.right;
            traveled = 0f;
            phase = Phase.Outgoing;
            FaceAlong(dir.x);
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
            float stepLen = speed * Time.fixedDeltaTime;

            // Stop at the first ground/wall along this step; otherwise advance until maxRange.
            RaycastHit2D hit = Physics2D.Raycast(myRigidbody.position, dir, stepLen, groundLayerMask);
            if (hit.collider != null) {
                myRigidbody.MovePosition(hit.point);
                EnterEmbedded();
                return;
            }

            traveled += stepLen;
            if (traveled >= maxRange) {
                EnterEmbedded();
                return;
            }

            myRigidbody.MovePosition(myRigidbody.position + dir * stepLen);
        }

        private void EnterEmbedded() {
            phase = Phase.Embedded;
            embedTimer = returnDelay;
        }

        private void TickEmbedded() {
            embedTimer -= Time.fixedDeltaTime;
            if (embedTimer <= 0f) {
                phase = Phase.Returning;
            }
        }

        private void TickReturning() {
            Vector2 target = returnTarget != null ? (Vector2)returnTarget.position : myRigidbody.position;
            Vector2 toSocket = target - myRigidbody.position;
            float stepLen = returnSpeed * Time.fixedDeltaTime;

            if (toSocket.magnitude <= stepLen) {
                myRigidbody.MovePosition(target);
                phase = Phase.Idle;
                Returned?.Invoke();
                return;
            }

            Vector2 step = toSocket.normalized * stepLen;
            FaceAlong(step.x);
            myRigidbody.MovePosition(myRigidbody.position + step);
        }

        private void FaceAlong(float x) {
            if (facing != null) {
                facing.SetByX(x);
            }
        }
    }
}
