using System;
using Core.Components.Extensions;
using UnityEngine;
using UnityEngine.Events;

namespace Core.Components.Collisions {
    /// <summary>
    /// Component that invokes a UnityEvent when a Collider2D enters its trigger.
    /// Can be filtered by tag and layer.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CollisionEnterComponent : MonoBehaviour {
        [SerializeField]
        private LayerMask layer = ~0;

        [SerializeField]
        private CollisionEnterEvent action;

        private void OnCollisionEnter2D(Collision2D other) {
            if (!layer.Contains(other.gameObject)) {
                return;
            }

            if (action != null) {
                action.Invoke(other.gameObject);
            } else {
                Debug.LogWarning("Action is not set");
            }
        }
    }

    [Serializable]
    public class CollisionEnterEvent : UnityEvent<GameObject> {
    }
}
