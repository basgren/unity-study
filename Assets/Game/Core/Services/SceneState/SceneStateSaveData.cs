using System;
using System.Collections.Generic;

namespace Game.Core.Services.SceneState {
    /// <summary>
    /// JSON-serializable snapshot of the persistent-tier scene-state store. Unity's
    /// JsonUtility cannot serialize the runtime store (nested dictionaries of boxed
    /// primitives), so it is flattened into typed lists here. Each field carries a
    /// <see cref="FieldEntry.type"/> tag identifying which typed column holds its value.
    /// </summary>
    [Serializable]
    public class SceneStateSaveData {
        public const int TypeBool = 0;
        public const int TypeInt = 1;
        public const int TypeFloat = 2;
        public const int TypeString = 3;
        public const int TypeVector2 = 4;

        public List<SceneEntry> scenes = new();

        [Serializable]
        public class SceneEntry {
            public string sceneId;
            public List<ObjectEntry> objects = new();
        }

        [Serializable]
        public class ObjectEntry {
            public string saveId;
            public List<FieldEntry> fields = new();
        }

        [Serializable]
        public class FieldEntry {
            public string slot;
            public string key;
            public int type;

            // Only the column matching `type` is meaningful.
            public bool b;
            public int i;
            public float f;
            public string s;
            public float x;
            public float y;
        }
    }
}
