using System;
using UnityEngine;

namespace Background {
    public class ParallaxScroller : MonoBehaviour {
        [SerializeField]
        private Vector2 parallaxMultiplier = new Vector2(0.3f, 0.3f);

        private Transform cameraTransform;
        private Vector3 lastCameraPosition;
        private float startZ;

        private void Awake() {
            if (Camera.main == null) {
                throw new ArgumentException("Camera.main is null");
            }

            cameraTransform = Camera.main.transform;
            lastCameraPosition = cameraTransform.position;
            startZ = transform.position.z;
        }

        private void LateUpdate() {
            if (cameraTransform == null) {
                return;
            }

            Vector3 cameraDelta = cameraTransform.position - lastCameraPosition;

            Vector3 move = new Vector3(
                cameraDelta.x * parallaxMultiplier.x,
                cameraDelta.y * parallaxMultiplier.y,
                0f
            );

            transform.position += move;

            // Some recommendation to manually assign fixed Z value in case it starts drifting,
            // but not sure if it's really needed.
            // transform.position = new Vector3(transform.position.x, transform.position.y, startZ);

            lastCameraPosition = cameraTransform.position;
        }
    }
}
