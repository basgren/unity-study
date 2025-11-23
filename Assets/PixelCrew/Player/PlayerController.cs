using System;
using UnityEngine;

namespace PixelCrew.Player {
    public class PlayerController : MonoBehaviour {
        [SerializeField]
        private float speed = 2f;
        
        private InputActions input;
        private InputActions.PlayerActions actions;

        private void Awake() {
            input = new InputActions();
            actions = input.Player;
        }

        private void OnEnable() {
            actions.Enable();
        }

        private void OnDisable() {
            actions.Disable();
        }

        private void OnDestroy() {
            input.Dispose();
        }

        void Update() {
            // We won't use InputSystem events, as order of their invocation is not guaranteed, but
            // in case we want to check button combinations, it's easier to check them manually.
            // Using events are better for UI controls.
            Vector2 dir = actions.Move.ReadValue<Vector2>().normalized;
            
            transform.position += new Vector3(dir.x, dir.y, 0) * (speed * Time.deltaTime);
        }
    }
}
