using UnityEngine;

namespace Components {
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
