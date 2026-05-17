#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Game.Features.Portals.Common.Editor {
    /// <summary>
    /// Central directory of portal kinds. Each kind self-registers from an
    /// <c>[InitializeOnLoad]</c> stub at editor load. The shared drawers, validators,
    /// project updaters and change-id window look up the right kind here based on the
    /// owning component type, so Common code never references concrete Door/Entrance types.
    /// </summary>
    public static class PortalKindRegistry {
        private static readonly List<PortalKind> kinds = new List<PortalKind>();
        private static readonly Dictionary<Type, PortalKind> byType = new Dictionary<Type, PortalKind>();

        public static IReadOnlyList<PortalKind> All => kinds;

        public static void Register(PortalKind kind) {
            if (kind == null) {
                return;
            }

            if (byType.ContainsKey(kind.ComponentType)) {
                return;
            }

            kinds.Add(kind);
            byType[kind.ComponentType] = kind;

            // The scene-reference repair tool walks every component type that owns a PortalLink.
            // Wiring it here keeps each kind's [InitializeOnLoad] stub down to one Register call.
            PortalSceneReferenceRepair.RegisterPortalType(kind.ComponentType);
        }

        public static PortalKind GetForComponent(UnityEngine.Object obj) {
            return obj == null ? null : GetForType(obj.GetType());
        }

        public static PortalKind GetForType(Type t) {
            if (t == null) {
                return null;
            }

            byType.TryGetValue(t, out var k);
            return k;
        }
    }
}
#endif
