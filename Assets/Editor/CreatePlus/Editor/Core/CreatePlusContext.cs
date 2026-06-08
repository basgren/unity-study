using UnityEngine;

namespace CreatePlus.Core {
    /// <summary>
    /// Describes where and how a command is being executed (which folder, which selection, where the
    /// palette was opened from). UI-independent. Built by the entry points (menu items) before the
    /// window is shown.
    /// </summary>
    public sealed class CreatePlusContext {
        /// <summary>Project-relative folder that new assets should be created in (e.g. "Assets/Game").</summary>
        public string TargetFolderAssetPath { get; set; } = "Assets";

        /// <summary>Currently selected GameObject when opened from Hierarchy/Scene. May be null.</summary>
        public GameObject SelectedGameObject { get; set; }

        /// <summary>Mouse position in screen space when available, used to place the palette.</summary>
        public Vector2 MousePosition { get; set; }

        /// <summary>True when opened from the Project window.</summary>
        public bool OpenedFromProject { get; set; }

        /// <summary>True when opened from the Hierarchy window (future).</summary>
        public bool OpenedFromHierarchy { get; set; }

        /// <summary>True when opened from the Scene view (future).</summary>
        public bool OpenedFromSceneView { get; set; }

        /// <summary>A neutral, project-root context used as a safe fallback.</summary>
        public static CreatePlusContext Empty {
            get {
                return new CreatePlusContext {
                    TargetFolderAssetPath = "Assets",
                    OpenedFromProject = true
                };
            }
        }

        /// <summary>Short human-readable description of the current target, for the context badge.</summary>
        public string DescribeTarget() {
            if (string.IsNullOrEmpty(TargetFolderAssetPath)) {
                return "Assets";
            }

            return TargetFolderAssetPath;
        }
    }
}
