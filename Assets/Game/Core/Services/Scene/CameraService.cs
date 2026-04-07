using Cinemachine;
using UnityEngine;

namespace Game.Core.Services.Scene {
    /// <summary>
    /// Provides camera utility operations used by gameplay and scene transitions.
    /// </summary>
    public class CameraService : MonoBehaviour {
        private Transform pendingTarget;
        private Vector3 pendingPositionDelta;

        private void LateUpdate() {
            TryApplyPendingTeleports();
        }

        /// <summary>
        /// Notifies the camera system that <paramref name="target"/> was teleported by <paramref name="positionDelta"/>.
        /// If no matching Cinemachine camera is ready yet, the request is queued and retried.
        /// </summary>
        public void NotifyTargetTeleported(Transform target, Vector3 positionDelta) {
            if (target == null || positionDelta.sqrMagnitude <= Mathf.Epsilon) {
                return;
            }

            if (TryNotifyTargetTeleported(target, positionDelta)) {
                return;
            }

            QueuePendingTeleport(target, positionDelta);
        }

        /// <summary>
        /// Tries to apply all queued teleport notifications.
        /// Returns true when no pending notification remains.
        /// </summary>
        public bool TryApplyPendingTeleports() {
            if (pendingTarget == null) {
                return true;
            }

            if (pendingPositionDelta.sqrMagnitude <= Mathf.Epsilon) {
                ClearPendingTeleport();
                return true;
            }

            if (!TryNotifyTargetTeleported(pendingTarget, pendingPositionDelta)) {
                return false;
            }

            ClearPendingTeleport();
            return true;
        }

        private void QueuePendingTeleport(Transform target, Vector3 positionDelta) {
            if (pendingTarget == target) {
                pendingPositionDelta += positionDelta;
            } else {
                pendingTarget = target;
                pendingPositionDelta = positionDelta;
            }
        }

        private void ClearPendingTeleport() {
            pendingTarget = null;
            pendingPositionDelta = Vector3.zero;
        }

        private bool TryNotifyTargetTeleported(Transform target, Vector3 positionDelta) {
            var cinemachineCore = CinemachineCore.Instance;
            if (cinemachineCore == null) {
                return false;
            }

            bool applied = false;
            int cameraCount = cinemachineCore.VirtualCameraCount;
            for (int i = 0; i < cameraCount; i++) {
                var cam = cinemachineCore.GetVirtualCamera(i);
                if (cam == null) {
                    continue;
                }

                if (cam.Follow != target && cam.LookAt != target) {
                    continue;
                }

                // Treat teleport as a cut and drop old damping state.
                cam.PreviousStateIsValid = false;
                cam.OnTargetObjectWarped(target, positionDelta);
                applied = true;
            }

            return applied;
        }
    }
}
