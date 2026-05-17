#if UNITY_EDITOR
namespace Game.Features.Portals.Common.Editor {
    /// <summary>
    /// Shared validator output: an error message plus the Unity object it relates to (so the
    /// console entry pings the right asset when clicked).
    /// </summary>
    public readonly struct PortalValidationError {
        public readonly string Message;
        public readonly UnityEngine.Object Context;

        public PortalValidationError(string message, UnityEngine.Object context) {
            Message = message;
            Context = context;
        }
    }
}
#endif
