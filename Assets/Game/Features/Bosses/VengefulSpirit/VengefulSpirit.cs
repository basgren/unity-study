using Core.Components.Base2D;
using Game.Core.Components.Base2D;
using Game.Core.Components.Damage;
using Game.Features.Characters.PinkStar;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit {
    internal static class VengefulSpiritAnimKeys {
        public static readonly int IsCharging = Animator.StringToHash("isCharging");
        public static readonly int OnAttack = Animator.StringToHash("onAttack");
        public static readonly int OnDeath = Animator.StringToHash("onDeath");
        public static readonly int OnHit = Animator.StringToHash("onHit");
    }

    /// <summary>
    /// Identifies which cast action should fire when the cast animation reaches its
    /// effect frame. Several cast actions reuse the same cast animation; the controller
    /// remembers the pending one and dispatches via <see cref="VengefulSpirit.OnCastEffect"/>.
    /// </summary>
    internal enum VengefulSpiritCastAction {
        None,
        SpawnShield
    }

    /// <summary>
    /// Command describing desired Vengeful Spirit actions for a single tick.
    /// </summary>
    public struct VengefulSpiritCommand {
        public readonly int XDirection;   // -1 left, 0 idle, +1 right
        public readonly int YDirection;   // -1 down, 0 idle, +1 up
        public readonly bool Attack;      // one-shot melee thrust trigger
        public readonly bool SpawnShield; // one-shot cast: spawn the spectral shield

        public VengefulSpiritCommand(int xDirection, int yDirection, bool attack, bool spawnShield) {
            XDirection = xDirection;
            YDirection = yDirection;
            Attack = attack;
            SpawnShield = spawnShield;
        }
    }

    public abstract class VengefulSpiritControlSource : BaseControlSource<VengefulSpiritCommand> {
    }

    /// <summary>
    /// Flying boss controller. Reads commands from a pluggable <see cref="VengefulSpiritControlSource"/>,
    /// drives 2D movement via Rigidbody2D velocity, and forwards basic abilities to the animator.
    ///
    /// NOTES:
    /// - Rigidbody2D is expected to be Dynamic with gravityScale = 0 and Z-rotation frozen. Collisions
    ///   with the Ground layer are resolved by the physics engine.
    /// - Stage 1: this is input-driven only. A proper state machine + AI will be added later.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Facing2D))]
    public class VengefulSpirit : MonoBehaviour {
        [SerializeField]
        private VengefulSpiritControlSource controlSource;

        [SerializeField]
        private float moveSpeed = 4f;

        [Header("Attack")]
        [SerializeField]
        private float attackThrustSpeed = 10f;

        [Tooltip("How long the boss thrusts forward during an attack. Should match the Attack animation length.")]
        [SerializeField]
        private float attackThrustDuration = 1.2f;

        [Tooltip("Time to ramp thrust speed from the boss's current speed up to attackThrustSpeed.")]
        [SerializeField]
        private float attackThrustRampUpDuration = 0.2f;

        [Tooltip("How quickly the boss decelerates after the thrust ends. Higher = faster stop.")]
        [SerializeField]
        private float attackStopDamping = 8f;

        [Header("Prefabs")]
        [SerializeField]
        private GameObject swordPrefab;

        [SerializeField]
        private GameObject shieldPrefab;

        [SerializeField]
        private Transform shieldSpawnPoint;

        private Rigidbody2D myRigidbody;
        private Animator myAnimator;
        private Facing2D facing;
        private Damageable damageable;

        private bool isCasting;
        private VengefulSpiritCastAction pendingCastAction;
        private bool isAttacking;
        private bool isAttackDecelerating;
        private float attackElapsed;
        private float attackInitialSpeed;
        private bool isDead;
        private bool wasHitThisFrame;
        private bool diedThisFrame;

        private void Awake() {
            myRigidbody = GetComponent<Rigidbody2D>();
            myAnimator = GetComponent<Animator>();
            facing = GetComponent<Facing2D>();
            damageable = GetComponent<Damageable>();
        }

        private void Update() {
            CheckDamageState();

            if (isDead) {
                return;
            }

            ExecuteCommand();
        }

        private void CheckDamageState() {
            if (damageable == null) {
                return;
            }

            // Poll instead of subscribing: Damageable.OnHealthChanged fires before IsDead is set,
            // so an event-based path would miss the death frame.
            if (damageable.IsHitThisFrame) {
                wasHitThisFrame = true;
            }

            if (damageable.IsDead && !isDead) {
                isDead = true;
                diedThisFrame = true;
                isCasting = false;
                pendingCastAction = VengefulSpiritCastAction.None;
                isAttacking = false;
                isAttackDecelerating = false;
                attackElapsed = 0f;
                StopMovement();
            }
        }

        private void LateUpdate() {
            UpdateAnimator();
        }

        private void ExecuteCommand() {
            // Attack locks the boss into a self-driven thrust for attackThrustDuration,
            // then UpdateAttackThrust() damps velocity back to zero.
            if (isAttacking) {
                UpdateAttackThrust();
                return;
            }

            // Casting locks the boss until OnCastAnimationEnd() fires from the cast clip.
            if (isCasting) {
                StopMovement();
                return;
            }

            if (controlSource == null) {
                StopMovement();
                return;
            }

            VengefulSpiritCommand? command = controlSource.GetCommand();
            if (command == null) {
                StopMovement();
                return;
            }

            VengefulSpiritCommand value = command.Value;

            if (value.Attack) {
                BeginAttack();
                return;
            }

            if (value.SpawnShield) {
                BeginCast(VengefulSpiritCastAction.SpawnShield);
                return;
            }

            ApplyMovement(value.XDirection, value.YDirection);
        }

        private void BeginCast(VengefulSpiritCastAction action) {
            isCasting = true;
            pendingCastAction = action;
            StopMovement();
        }

        private void BeginAttack() {
            isAttacking = true;
            isAttackDecelerating = false;
            attackElapsed = 0f;
            attackInitialSpeed = myRigidbody.velocity.magnitude;
            isCasting = false;
            myAnimator.SetTrigger(VengefulSpiritAnimKeys.OnAttack);
            myRigidbody.velocity = facing.DirVector * attackInitialSpeed;
        }

        private void UpdateAttackThrust() {
            if (!isAttackDecelerating) {
                attackElapsed += Time.deltaTime;
                // Ramp from the speed the boss had when the attack began up to attackThrustSpeed,
                // then hold max speed. Driving velocity directly each frame keeps gravity out of the thrust.
                float speed = attackThrustSpeed;
                if (attackThrustRampUpDuration > 0f && attackElapsed < attackThrustRampUpDuration) {
                    float t = attackElapsed / attackThrustRampUpDuration;
                    speed = Mathf.Lerp(attackInitialSpeed, attackThrustSpeed, t);
                }
                myRigidbody.velocity = facing.DirVector * speed;
                if (attackElapsed >= attackThrustDuration) {
                    isAttackDecelerating = true;
                }
                return;
            }

            // Damp velocity toward zero — completes the attack with a smooth stop.
            Vector2 v = Vector2.Lerp(myRigidbody.velocity, Vector2.zero, attackStopDamping * Time.deltaTime);
            myRigidbody.velocity = v;

            if (v.sqrMagnitude < 0.0001f) {
                myRigidbody.velocity = Vector2.zero;
                isAttacking = false;
                isAttackDecelerating = false;
            }
        }

        /// <summary>
        /// Animation event hook. Called from the cast animation at the moment the
        /// queued cast action should take effect. Dispatches based on the action
        /// requested when the cast was initiated, then consumes the slot so a looping
        /// clip cannot fire the effect a second time.
        /// </summary>
        public void OnCastEffect() {
            VengefulSpiritCastAction action = pendingCastAction;
            pendingCastAction = VengefulSpiritCastAction.None;

            switch (action) {
                case VengefulSpiritCastAction.SpawnShield:
                    SpawnShield();
                    break;
            }
        }

        /// <summary>
        /// Animation event hook. Called at the end of the cast animation. Releases
        /// the cast lock so the boss can act again.
        /// </summary>
        public void OnCastAnimationEnd() {
            isCasting = false;
            pendingCastAction = VengefulSpiritCastAction.None;
        }

        private void SpawnShield() {
            if (shieldPrefab == null || shieldSpawnPoint == null) {
                return;
            }

            // Spawn unparented and follow via TransformFollow2D. Parenting under the boss
            // would mirror the shield sprite when the boss faces left (its localScale.x = -1),
            // and the shield's reflection must always render from the same side.
            GameObject instance = Instantiate(shieldPrefab, shieldSpawnPoint.position, Quaternion.identity);
            instance.AddComponent<TransformFollow2D>().Target = shieldSpawnPoint;
        }

        private void ApplyMovement(int xDir, int yDir) {
            Vector2 input = new Vector2(xDir, yDir);
            if (input.sqrMagnitude > 1f) {
                input.Normalize();
            }

            myRigidbody.velocity = input * moveSpeed;

            if (xDir != 0) {
                facing.SetByX(xDir);
            }
        }

        private void StopMovement() {
            myRigidbody.velocity = Vector2.zero;
        }

        private void UpdateAnimator() {
            myAnimator.SetBool(VengefulSpiritAnimKeys.IsCharging, isCasting);

            if (wasHitThisFrame) {
                wasHitThisFrame = false;
                myAnimator.SetTrigger(VengefulSpiritAnimKeys.OnHit);
            }

            if (diedThisFrame) {
                diedThisFrame = false;
                myAnimator.SetTrigger(VengefulSpiritAnimKeys.OnDeath);
            }
        }
    }
}