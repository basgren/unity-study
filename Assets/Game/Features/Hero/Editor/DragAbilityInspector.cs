using Editor;
using Game.Editor;
using Game.Features.Dynamic;
using UnityEditor;
using UnityEngine;

namespace Game.Features.Characters.Hero.Editor {
    [CustomEditor(typeof(DraggableBarrel))]
    public class DraggableBarrelInspector : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();
        }
    }
}
