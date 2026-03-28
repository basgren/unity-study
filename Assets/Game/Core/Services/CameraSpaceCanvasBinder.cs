using UnityEngine;

namespace Game.Core.Services {
    /// <summary>
    /// Binds a Canvas to the main camera in ScreenSpace-Camera mode at runtime.
    /// Attach to any Canvas that is loaded additively and needs a camera reference.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class CameraSpaceCanvasBinder : MonoBehaviour {
        [SerializeField] private float planeDistance = 10f;
        [SerializeField] private bool pixelPerfect = true;
        [SerializeField] private string sortingLayerName = "Default";

        private void Start() {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = planeDistance;
            canvas.pixelPerfect = pixelPerfect;
            canvas.sortingLayerName = sortingLayerName;
        }
    }
}
