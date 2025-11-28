using System;
using UnityEngine;

namespace Prefabs.Interactive.Portal {
    public class PortalController : MonoBehaviour {
        [SerializeField]
        private PortalDestController portalDest;

        private void OnTriggerEnter2D(Collider2D other) {
            if (other.CompareTag("Player") && portalDest != null) {
                other.transform.position = portalDest.transform.position;
            }
        }
    }
}
