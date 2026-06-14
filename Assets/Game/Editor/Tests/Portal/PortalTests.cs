using Game.Features.Portals.Portal;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Tests.Portals {
    public class PortalTests {
        private static Portal NewPortal() {
            var go = new GameObject("Portal", typeof(BoxCollider2D));
            return go.AddComponent<Portal>();
        }

        [Test]
        public void GetEntryPosition_NoEntryPoint_ReturnsOwnPosition() {
            var portal = NewPortal();
            portal.transform.position = new Vector3(3f, 4f, 0f);

            Assert.AreEqual(new Vector3(3f, 4f, 0f), portal.GetEntryPosition());

            Object.DestroyImmediate(portal.gameObject);
        }

        [Test]
        public void GetEntryPosition_WithEntryPoint_ReturnsEntryPointPosition() {
            var portal = NewPortal();
            portal.transform.position = new Vector3(3f, 4f, 0f);

            var entry = new GameObject("Entry").transform;
            entry.position = new Vector3(10f, 20f, 0f);

            // entryPoint is a private serialized field; set it via SerializedObject like the editor would.
            var so = new SerializedObject(portal);
            so.FindProperty("entryPoint").objectReferenceValue = entry;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(new Vector3(10f, 20f, 0f), portal.GetEntryPosition());

            Object.DestroyImmediate(entry.gameObject);
            Object.DestroyImmediate(portal.gameObject);
        }
    }
}
