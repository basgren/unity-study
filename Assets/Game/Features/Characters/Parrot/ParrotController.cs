using System;
using System.Collections.Generic;
using Core.Components.Collisions;
using Game.Core.Components.Base2D;
using Game.Core.Components.Collisions;
using Game.Core.Components.Damage;
using UnityEngine;

namespace Game.Features.Characters.Parrot {
    public enum ParrotMode {
        Free,
        Follow,
    }

    /// <summary>
    /// Controls parrot movement, facing, damage dealing, and provides
    /// detection/deploy/recall API. Movement commands come from <see cref="ParrotAI"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Facing2D))]
    public class ParrotController : MonoBehaviour {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 1.5f;
        [SerializeField] private float flySpeed = 5f;
        [SerializeField] private float attackFlySpeed = 8f;
        [SerializeField] private Vector2 followOffset = new(1f, 1.5f);

        [Header("Follow Smoothing")]
        [SerializeField] private float followSmoothTime = 0.3f;
        [SerializeField] private float offsetFlipSmoothTime = 0.6f;

        [Header("Detection")]
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private LayerMask obstacleLayers;

        [Header("References")]
        [SerializeField] private GameObject damagerObject;

        public ParrotMode Mode { get; set; }
        public float WalkSpeed => walkSpeed;
        public float FlySpeed => flySpeed;
        public float AttackFlySpeed => attackFlySpeed;
        public Vector2 FollowOffset => followOffset;

        /// <summary>
        /// Fired when the parrot finishes landing and should return to inventory.
        /// </summary>
        public event Action OnRecalled;

        private Rigidbody2D rb;
        private Facing2D facing;
        private Animator animator;
        private GameObject collectableObject;

        private Collider2D visionCollider;
        private LayerCheck visionLayerCheck;
        private ContactFilter2D enemyFilter;
        private readonly List<Collider2D> overlapResults = new();

        // Cached from the damagerObject's collider before it's disabled.
        // Used for distance-based range checks while the damager is inactive.
        private float damagerRange;
        private Collider2D damagerCollider;

        // Smooth follow state
        private Vector2 smoothVelocity;
        private float currentOffsetX;
        private float offsetXVelocity;

        private static readonly int IsRunning = Animator.StringToHash("isRunning");
        private static readonly int IsFlyMoving = Animator.StringToHash("isFlyMoving");
        private static readonly int IsFlying = Animator.StringToHash("isFlying");
        private static readonly int OnAttack = Animator.StringToHash("onAttack");

        private void Awake() {
            rb = GetComponent<Rigidbody2D>();
            facing = GetComponent<Facing2D>();
            animator = GetComponent<Animator>();

            var collectableTransform = transform.Find("Collectable");
            if (collectableTransform != null) {
                collectableObject = collectableTransform.gameObject;
            }

            var visionGo = transform.Find("Vision");
            if (visionGo != null) {
                visionCollider = visionGo.GetComponent<Collider2D>();
                visionLayerCheck = visionGo.GetComponent<LayerCheck>();
            }

            enemyFilter = new ContactFilter2D();
            enemyFilter.useLayerMask = true;
            enemyFilter.layerMask = enemyLayer;
            enemyFilter.useTriggers = true;

            // Cache the damager's effective range before disabling it
            if (damagerObject != null) {
                damagerCollider = damagerObject.GetComponent<Collider2D>();
                if (damagerCollider != null) {
                    damagerRange = damagerCollider.bounds.extents.magnitude;
                }
            }

            currentOffsetX = followOffset.x;

            DisableDamager();
        }

        private void LateUpdate() {
            UpdateAnimator();
        }

        // ---=== Public API ===---

        /// <summary>
        /// Activates the parrot in the specified mode.
        /// </summary>
        public void Deploy(ParrotMode mode) {
            Mode = mode;
            gameObject.SetActive(true);
            ApplyGravityForMode(mode);
            SetCollectableActive(mode == ParrotMode.Free);
        }

        /// <summary>
        /// Signals that landing is complete and the parrot should be returned to inventory.
        /// Called by <see cref="ParrotAI"/> when the Landing state finishes.
        /// </summary>
        public void Recall() {
            OnRecalled?.Invoke();
        }

        // ---=== Movement ===---

        /// <summary>
        /// Ground walk movement for Free/Roaming mode.
        /// </summary>
        public void SetMoveDirection(int xDir) {
            var vx = Mathf.Sign(xDir) * walkSpeed;
            if (xDir == 0) {
                vx = 0f;
            }

            rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
            facing.SetByX(xDir);
        }

        /// <summary>
        /// Smoothly follows a target position using SmoothDamp.
        /// Used for Following state to produce a floating, non-jittery movement.
        /// </summary>
        public void SmoothFlyToward(Vector2 target) {
            var current = (Vector2)transform.position;
            var smoothed = Vector2.SmoothDamp(current, target, ref smoothVelocity, followSmoothTime);
            rb.linearVelocity = (smoothed - current) / Time.deltaTime;

            // Only change facing when there's meaningful horizontal movement
            if (Mathf.Abs(smoothVelocity.x) > 0.1f) {
                facing.SetByX(smoothVelocity.x);
            }
        }

        /// <summary>
        /// Fly toward a world-space target at the given speed.
        /// Used for Attack and Return states where direct movement is needed.
        /// </summary>
        public void FlyToward(Vector2 target, float speed) {
            var delta = target - (Vector2)transform.position;
            if (delta.sqrMagnitude < 0.01f) {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            rb.linearVelocity = delta.normalized * speed;
            facing.SetByX(delta.x);
        }

        /// <summary>
        /// Stops all movement immediately and resets smooth follow state.
        /// </summary>
        public void Stop() {
            rb.linearVelocity = Vector2.zero;
            smoothVelocity = Vector2.zero;
        }

        /// <summary>
        /// Switches physics between grounded (gravity on) and flying (gravity off).
        /// </summary>
        public void ApplyGravityForMode(ParrotMode mode) {
            rb.gravityScale = mode == ParrotMode.Free ? 1f : 0f;
        }

        /// <summary>
        /// Computes the follow target position with smoothed offset flip.
        /// When the player changes direction, the horizontal offset transitions
        /// gradually instead of snapping to the other side.
        /// </summary>
        public Vector2 GetSmoothedFollowPosition(Vector2 playerPos, int heroFacingSign) {
            // Parrot sits behind the player — offset is opposite to hero's facing
            var targetOffsetX = -heroFacingSign * followOffset.x;
            currentOffsetX = Mathf.SmoothDamp(
                currentOffsetX, targetOffsetX, ref offsetXVelocity, offsetFlipSmoothTime);

            return playerPos + new Vector2(currentOffsetX, followOffset.y);
        }

        // ---=== Facing ===---

        public void SetFacing(float xDir) {
            facing.SetByX(xDir);
        }

        public int GetFacingSign() {
            return facing.DirSign;
        }

        // ---=== Damage ===---

        public void EnableDamager() {
            if (damagerObject == null) {
                return;
            }

            damagerObject.SetActive(true);

            // Force OnTriggerEnter2D for already-overlapping colliders
            // by briefly toggling the collider off and on.
            if (damagerCollider != null) {
                damagerCollider.enabled = false;
                damagerCollider.enabled = true;
            }
        }

        public void DisableDamager() {
            if (damagerObject != null) {
                damagerObject.SetActive(false);
            }
        }

        public void TriggerAttackAnimation() {
            if (animator != null) {
                animator.SetTrigger(OnAttack);
            }
        }

        /// <summary>
        /// Returns true when the target is within the DamageTrigger's effective range.
        /// Uses distance check instead of collider overlap so it works while the damager
        /// object is disabled.
        /// </summary>
        public bool IsInDamagerRange(Vector2 targetPos) {
            return damagerRange > 0f
                   && Vector2.Distance(transform.position, targetPos) <= damagerRange;
        }

        // ---=== Collectable ===---

        public void SetCollectableActive(bool active) {
            if (collectableObject != null) {
                collectableObject.SetActive(active);
            }
        }

        // ---=== Detection ===---

        /// <summary>
        /// Finds the closest enemy within the vision range that has a clear line of sight
        /// (no Ground or DynamicObjects blocking). Returns null if none found.
        /// </summary>
        public Damageable FindVisibleEnemy() {
            if (visionLayerCheck == null || !visionLayerCheck.IsColliding()) {
                return null;
            }

            overlapResults.Clear();
            visionCollider.Overlap(enemyFilter, overlapResults);

            Damageable closest = null;
            var closestDist = float.MaxValue;

            foreach (var col in overlapResults) {
                if (col == null) {
                    continue;
                }

                var targetPos = col.bounds.center;
                var origin = transform.position;

                // Line-of-sight: check for obstacles between parrot and enemy
                var losHit = Physics2D.Linecast(origin, targetPos, obstacleLayers);
                if (losHit.collider != null) {
                    continue;
                }

                var dist = Vector2.Distance(origin, targetPos);
                if (dist >= closestDist) {
                    continue;
                }

                var damageable = col.GetComponent<Damageable>();
                if (damageable == null) {
                    damageable = col.GetComponentInParent<Damageable>();
                }

                if (damageable != null && !damageable.IsDead) {
                    closest = damageable;
                    closestDist = dist;
                }
            }

            return closest;
        }

        // ---=== Animation ===---

        private void UpdateAnimator() {
            if (animator == null) {
                return;
            }

            var isFlying = Mode == ParrotMode.Follow;
            animator.SetBool(IsFlying, isFlying);
            animator.SetBool(IsRunning, !isFlying && Mathf.Abs(rb.linearVelocity.x) > 0.01f);
            animator.SetBool(IsFlyMoving, Mathf.Abs(rb.linearVelocity.x) > 0.5f);
        }
    }
}
