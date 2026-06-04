using System.Collections.Generic;
using Core;
using Core.Audio;
using Core.Utils;
using Game.Components.Abilities;
using Game.Core.Bootstrap;
using Game.Core.Utils;
using Game.Features.Props.Barrel;
using UnityEngine;

namespace Game.Features.Dynamic {
    /// <summary>
    /// Identity/state for a draggable barrel. Motion is handled by <see cref="BarrelMotor"/>
    /// (kinematic, raycast-driven); this component owns stacking detection, highlighting and
    /// sounds. There are no physics joints — the player↔barrel and barrel↔barrel links are
    /// logical, owned by the drag coordinator.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(BarrelHighlighter))]
    [RequireComponent(typeof(BarrelMotor))]
    public class DraggableBarrel : MonoBehaviour {
        [Header("Sounds")]
        [SerializeField]
        private AudioCue draggingSound;

        [SerializeField]
        private AudioCue fallSound;

        public Rigidbody2D Body { get; private set; }
        public BoxCollider2D Collider { get; private set; }
        public BarrelMotor Motor => motor;

        public bool IsGrounded => motor.IsGrounded;
        public LayerMask ObstacleMask => motor.ObstacleMask;

        /// <summary>This barrel's own colliders, used by the coordinator to exclude convoy members from casts.</summary>
        public Collider2D[] BodyColliders { get; private set; }

        private BarrelHighlighter highlighter;
        private BarrelMotor motor;
        private MultiRayCaster topRayCaster;
        private bool isDragged;

        private IAudioLoopHandle dragSoundHandle;

        private void Awake() {
            Body = GetComponent<Rigidbody2D>();
            Collider = GetComponent<BoxCollider2D>();
            highlighter = GetComponent<BarrelHighlighter>();
            motor = GetComponent<BarrelMotor>();

            // All of the barrel's own colliders. The coordinator excludes these from convoy casts
            // so members don't block each other or the player. Capsule-agnostic: works whether or
            // not the legacy rounded-corner capsule is still present.
            BodyColliders = GetComponents<Collider2D>();

            // Upward check used to find a barrel resting on top (stacking). Uses the motor's
            // obstacle mask, excludes our own colliders, and reaches a couple of pixels so it
            // detects a barrel sitting just above.
            topRayCaster = new MultiRayCaster(Collider, motor.ObstacleMask)
                .WithDirection(Direction2D.Up)
                .WithRayCount(3)
                .ExcludeColliders(BodyColliders)
                .WithRayLength(2f * CoreConst.PixelSize);
        }

        private void FixedUpdate() {
            topRayCaster.Update();
        }

        private void Update() {
            UpdateDragSound();
        }

        public void SetDragged(bool dragged) {
            if (dragged == isDragged) {
                return;
            }

            isDragged = dragged;
            motor.SetDragged(dragged);

            if (!dragged) {
                StopDragSound();
            }
        }

        public void SetHighlighted(BarrelHighlightMode mode) {
            highlighter.SetHighlighted(mode);
        }

        public List<T> GetDraggablesAbove<T>() where T : DraggableBarrel {
            return topRayCaster.GetHitComponents<T>();
        }

        /// <summary>Called by <see cref="BarrelMotor"/> when the barrel lands after a fall.</summary>
        public void OnLanded() {
            if (fallSound != null) {
                G.Audio.PlayAt(fallSound, transform.position);
            }
        }

        private void UpdateDragSound() {
            if (draggingSound == null) {
                return;
            }

            bool isMoving = Mathf.Abs(motor.LastHorizontalMove) > 0.0001f;

            if (isDragged && dragSoundHandle == null && isMoving) {
                dragSoundHandle = G.Audio.PlayLoopFollow(draggingSound, transform, is3D: true);
            } else if ((!isDragged || !isMoving) && dragSoundHandle != null) {
                StopDragSound();
            }
        }

        private void StopDragSound() {
            if (dragSoundHandle != null) {
                dragSoundHandle.Stop();
                dragSoundHandle = null;
            }
        }

        private void OnDrawGizmosSelected() {
            topRayCaster?.DrawGizmos();
        }
    }
}
