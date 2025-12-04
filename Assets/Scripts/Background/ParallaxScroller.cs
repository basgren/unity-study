using System;
using Cinemachine;
using UnityEngine;

namespace Background {
    public class ParallaxScroller : MonoBehaviour {
        [SerializeField] 
        private Vector2 parallaxMultiplier = new Vector2(0.3f, 0.3f);

        private CinemachineBrain brain;
        private Transform cam;

        private Vector3 startPos;
        private Vector3 camStartPos;

        private void Awake()
        {
            brain = Camera.main.GetComponent<CinemachineBrain>();
            cam = brain.transform;

            transform.position = new Vector3(cam.position.x, cam.position.y, transform.position.z);
            startPos = transform.position;
            camStartPos = cam.position;

            // подписка на событие
            CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
        }

        private void OnDestroy()
        {
            CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        }

        private void OnCameraUpdated(CinemachineBrain b)
        {
            // камера полностью обновлена → можно делать параллакс
            Vector3 camOffset = cam.position - camStartPos;

            Vector3 offset = new Vector3(
                camOffset.x * parallaxMultiplier.x,
                camOffset.y * parallaxMultiplier.y,
                0f
            );

            transform.position = startPos + offset;
        }
    }
}
