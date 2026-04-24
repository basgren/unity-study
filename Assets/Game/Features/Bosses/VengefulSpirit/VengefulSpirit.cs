using Core.Components.Base2D;
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
    /// Command describing desired Vengeful Spirit actions for a single tick.
    /// </summary>
    public struct VengefulSpiritCommand {
        public readonly int XDirection; // -1 left, 0 idle, +1 right
        public readonly int YDirection; // -1 down, 0 idle, +1 up
        public readonly bool Attack;    // one-shot melee trigger
        public readonly bool Cast;      // held while charging/casting

        public VengefulSpiritCommand(int xDirection, int yDirection, bool attack, bool cast) {
            XDirection = xDirection;
            YDirection = yDirection;
            Attack = attack;
            Cast = cast;
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

        private Rigidbody2D myRigidbody;
        private Animator myAnimator;
        private Facing2D facing;
        private Damageable damageable;

        private bool isCasting;
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
                StopMovement();
            }
        }

        private void LateUpdate() {
            UpdateAnimator();
        }

        private void ExecuteCommand() {
            if (controlSource == null) {
                StopMovement();
                isCasting = false;
                return;
            }

            VengefulSpiritCommand? command = controlSource.GetCommand();
            if (command == null) {
                StopMovement();
                isCasting = false;
                return;
            }

            VengefulSpiritCommand value = command.Value;

            // Casting freezes movement — feels closer to a "charging" cast.
            isCasting = value.Cast;
            if (isCasting) {
                StopMovement();
            } else {
                ApplyMovement(value.XDirection, value.YDirection);
            }

            if (value.Attack) {
                myAnimator.SetTrigger(VengefulSpiritAnimKeys.OnAttack);
            }
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