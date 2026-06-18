using Unity.Cinemachine;
using UnityEngine;

namespace Game.Core.Components.Camera {
    /// <summary>
    /// Moves this GameObject's XY position in proportion to the main camera's movement,
    /// creating a parallax scrolling effect.
    ///
    /// By default, speed is derived from the layer's Z depth:
    /// Z = 0 → speed 0 (layer stays still); Z = MaxLayerDistance → speed 1 (moves with camera).
    /// Speed can be overridden manually via <see cref="overrideSpeed"/>.
    ///
    /// Set <see cref="parallaxOrigin"/> to an empty GameObject at the level's design-time camera
    /// centre so layer positions are correct regardless of player spawn location.
    /// </summary>
    [ExecuteAlways]
    public class ParallaxLayer : MonoBehaviour {
        /// <summary>
        /// Layers at this Z distance move fully with the camera (speed = 1).
        /// Layers at Z = 0 do not move (speed = 0, maximum parallax effect).
        /// </summary>
        private const float MaxLayerDistance = 30f;

        [SerializeField] private bool overrideSpeed;

        /// <summary>Used only when <see cref="overrideSpeed"/> is true.</summary>
        [SerializeField] private Vector2 speed = Vector2.one;

        [SerializeField] private bool showGizmo = true;

        /// <summary>
        /// Confiner used by the editor gizmo to visualise the layer's travel bounds.
        /// </summary>
        [SerializeField] private PolygonCollider2D confiner;

        /// <summary>
        /// Transform marking the camera's design-time origin for this scene.
        /// Place an empty GameObject at the level's canonical camera position
        /// (e.g. level centre). If unset, world origin (0, 0) is used.
        /// </summary>
        [SerializeField] private Transform parallaxOrigin;

        private Transform cam;
        private Vector3 basePos;
        private Vector2 effectiveSpeed;

        private void Awake() {
            if (!Application.isPlaying) {
                return;
            }

            var mainCam = UnityEngine.Camera.main;
            var brain = mainCam.GetComponent<CinemachineBrain>();
            cam = brain.transform;

            if (!overrideSpeed) {
                float f = Mathf.Clamp01(Mathf.Abs(transform.position.z) / MaxLayerDistance);
                effectiveSpeed = new Vector2(f, f);
            } else {
                effectiveSpeed = speed;
            }

            // If preview was active, transform.position is displaced; use the stored authored position.
            Vector3 authoredPos = transform.position;
            Vector3 originPos = parallaxOrigin != null ? parallaxOrigin.position : Vector3.zero;
            basePos = new Vector3(
                authoredPos.x - originPos.x * effectiveSpeed.x,
                authoredPos.y - originPos.y * effectiveSpeed.y,
                authoredPos.z
            );

            CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
        }

        private void OnDestroy() {
            if (!Application.isPlaying) {
                return;
            }
            CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        }

        private void OnCameraUpdated(CinemachineBrain b) {
            transform.position = new Vector3(
                basePos.x + cam.position.x * effectiveSpeed.x,
                basePos.y + cam.position.y * effectiveSpeed.y,
                basePos.z
            );
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected() {
            if (!showGizmo || confiner == null) {
                return;
            }

            var sceneCam = UnityEditor.SceneView.lastActiveSceneView?.camera;
            if (sceneCam == null) {
                return;
            }

            Vector2 gizmoSpeed;
            if (!overrideSpeed) {
                float f = Mathf.Clamp01(Mathf.Abs(transform.position.z) / MaxLayerDistance);
                gizmoSpeed = new Vector2(f, f);
            } else {
                gizmoSpeed = speed;
            }

            float orthoSize = sceneCam.orthographicSize;
            float aspect = sceneCam.aspect;
            if (Application.isPlaying && UnityEngine.Camera.main != null) {
                var gameCam = UnityEngine.Camera.main;
                orthoSize = gameCam.orthographicSize;
                aspect = gameCam.aspect;
            }

            float vpW = 2f * orthoSize * aspect;
            float vpH = 2f * orthoSize;

            Vector2 layerXY = transform.position;
            Vector2 cc = confiner.bounds.center;
            var points = confiner.GetPath(0);

            // Visible-motion factor. In the layer's local frame the layer is static and
            // the camera moves across it at (1 - speed) * worldCameraDelta — that is the
            // motion the player actually sees on this layer. For a sky layer (speed = 1)
            // this factor is 0 (nothing visibly moves); for a Z = 0 layer it is 1
            // (full visible motion).
            float vx = 1f - gizmoSpeed.x;
            float vy = 1f - gizmoSpeed.y;

            // 1. Confiner polygon mapped into the layer's local frame (magenta).
            // Each confiner point is scaled by the visible-motion factor around the
            // confiner centre, then re-centred on the layer. This draws the path the
            // camera actually traces across this layer from the player's point of view.
            Gizmos.color = new Color(1f, 0.3f, 1f, 0.7f);
            for (int i = 0; i < points.Length; i++) {
                Vector3 wpa = confiner.transform.TransformPoint(points[i]);
                Vector3 wpb = confiner.transform.TransformPoint(points[(i + 1) % points.Length]);
                var a = new Vector3(
                    layerXY.x + (wpa.x - cc.x) * vx,
                    layerXY.y + (wpa.y - cc.y) * vy,
                    transform.position.z);
                var b = new Vector3(
                    layerXY.x + (wpb.x - cc.x) * vx,
                    layerXY.y + (wpb.y - cc.y) * vy,
                    transform.position.z);
                Gizmos.DrawLine(a, b);
            }

            // 2. Camera viewport frame, also scaled by the visible-motion factor (white).
            // A sky layer (speed = 1) collapses to a point; a static layer (speed = 0)
            // shows the full viewport. This matches the confiner gizmo so both visually
            // shrink together for farther layers.
            Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
            Gizmos.DrawWireCube(
                new Vector3(layerXY.x, layerXY.y, transform.position.z),
                new Vector3(vpW * vx, vpH * vy, 0f));
        }
#endif
    }
}
