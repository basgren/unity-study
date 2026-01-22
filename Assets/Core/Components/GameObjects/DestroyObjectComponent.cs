using UnityEngine;

namespace Core.Components.GameObjects {
    public class DestroyObjectComponent : MonoBehaviour {
        [SerializeField]
        private GameObject objectToDestroy;

        public void DestroySelf() => Destroy(gameObject);

        public void DestroyObject() {
            if (objectToDestroy != null) {
                Destroy(objectToDestroy);
            }
        }
    }
}
