using UnityEngine;

namespace Game.Core.Services {
    [CreateAssetMenu(menuName = "Config/Asset Refs", fileName = "AssetRefs")]
    public sealed class AssetRefs : ScriptableObject {
        [SerializeField]
        private GameObject infoBubblePrefab;

        public GameObject InfoBubblePrefab => infoBubblePrefab;
    }
}
