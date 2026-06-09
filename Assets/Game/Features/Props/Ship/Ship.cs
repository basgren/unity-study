using System;
using Game.Core.Components.Animation;
using UnityEngine;

namespace Game.Features.Props.Ship {
    public class Ship : MonoBehaviour {

        [Header("Sailing")]
        [SerializeField]
        private MultiStateSpriteAnimator sailAnim;
        
        [SerializeField, Tooltip("Cruise speed (world units/sec) the ship settles at once fully under sail.")]
        private float sailSpeed = 3f;

        [SerializeField, Tooltip("Seconds to ramp from a standstill up to full sail speed.")]
        private float sailAccelTime = 4f;

        [Header("Bobbing")]
        [SerializeField, Tooltip("Vertical bob height in world units (peak offset above/below the start height).")]
        private float bobAmplitude = 0.1f;

        [SerializeField, Tooltip("Seconds for one full up-and-down bob cycle.")]
        private float bobPeriod = 2f;

        private bool sailing;
        private float currentSpeed;
        private Rigidbody2D body;
        private Vector2 sailOrigin;
        private float sailedX;
        private float bobTime;
        
        private void Awake() {
            body = GetComponent<Rigidbody2D>();
        }

        private void Start() {
            sailAnim.SetClip("down");
        }
        
        // Physics-driven movement, so it runs in FixedUpdate and goes through the Rigidbody2D.
        private void FixedUpdate() {
            if (!sailing) {
                return;
            }

            float dt = Time.fixedDeltaTime;

            if (sailAccelTime > 0f) {
                currentSpeed = Mathf.MoveTowards(currentSpeed, sailSpeed, (sailSpeed / sailAccelTime) * dt);
            } else {
                currentSpeed = sailSpeed;
            }

            sailedX += currentSpeed * dt;
            bobTime += dt;

            // Sail right from the origin captured at BeginSailing, with a gentle vertical bob.
            float bobY = bobPeriod > 0f
                ? bobAmplitude * Mathf.Sin((2f * Mathf.PI / bobPeriod) * bobTime)
                : 0f;
            Vector2 target = new Vector2(sailOrigin.x + sailedX, sailOrigin.y + bobY);

            if (body != null) {
                body.MovePosition(target);
            } else {
                transform.position = new Vector3(target.x, target.y, transform.position.z);
            }
        }

        /// <summary>Raises the sails (visual only).</summary>
        public void SetSail() {
            sailAnim.SetClip("up");
        }

        /// <summary>
        /// Begins self-propelled sailing to the right, accelerating from a standstill up to
        /// <see cref="sailSpeed"/> over <see cref="sailAccelTime"/> seconds.
        /// </summary>
        public void BeginSailing() {
            // Implicit Vector3 -> Vector2 conversion captures the current XY as the sail origin.
            Vector2 origin = transform.position;
            if (body != null) {
                origin = body.position;
            }

            sailOrigin = origin;
            sailedX = 0f;
            bobTime = 0f;
            sailing = true;
        }
    }
}
