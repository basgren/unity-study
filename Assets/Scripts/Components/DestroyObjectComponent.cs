using UnityEngine;

namespace Components {
    public class DestroyObjectComponent : MonoBehaviour {
        [SerializeField] private GameObject objectToDestroy;

        public void DestroyObject() => Destroy(objectToDestroy);
    }
}
