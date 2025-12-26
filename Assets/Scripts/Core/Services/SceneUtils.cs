using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Services {
    public static class SceneUtils {
        public static GameObject GetOrCreateObject(string gameObjectName) {
            return GameObject.Find(gameObjectName) ?? new GameObject(gameObjectName);
        }
        
        public static GameObject GetOrCreateRootObject(string name, Action<GameObject> onCreateCallback = null) {
            return GetOrCreateObject(name, null, false, onCreateCallback);
        }
        
        public static GameObject GetOrCreateObject(
            string name,
            Transform parent,
            bool worldPositionStays = false,
            Action<GameObject> onCreateCallback = null
        ) {
            if (string.IsNullOrEmpty(name)) {
                Debug.LogError("GetOrCreate: name is null or empty.");
                return null;
            }

            var found = parent == null
                ? FindRootOnly(name)
                : FindDirectChild(parent, name);
            
            if (found != null) {
                return found;
            }

            var created = new GameObject(name);

            if (parent != null) {
                created.transform.SetParent(parent, worldPositionStays);
            }

            if (onCreateCallback != null) {
                onCreateCallback(created);
            }

            return created;
        }
        
        private static GameObject FindRootOnly(string name) {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();

            foreach (var go in roots) {
                if (go.name == name) {
                    return go;
                }
            }

            return null;
        }
        
        private static GameObject FindDirectChild(Transform parent, string name) {
            for (var i = 0; i < parent.childCount; i++) {
                var child = parent.GetChild(i);

                if (child.name == name) {
                    return child.gameObject;
                }
            }

            return null;
        }
    }
}
