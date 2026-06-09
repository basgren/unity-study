using UnityEngine;

namespace Game.Core.Components.Base2D {
    /// <summary>
    /// Logical left/right orientation for a 2D object.
    /// </summary>
    public enum FacingDir {
        Left = -1,
        Right = 1
    }

    /// <summary>
    /// Stores the current logical direction of the object and applies it to transform.localScale.x.
    /// 
    /// Main rules:
    /// - Dir is the single source of truth for orientation.
    /// - Direction is applied through localScale.x.
    /// - localScale.x should not be changed manually from other code.
    /// - In edit mode the orientation can also be previewed via OnValidate().
    /// </summary>
    [ExecuteAlways]
    public class Facing2D : MonoBehaviour {
        [SerializeField]
        private FacingDir dir = FacingDir.Right;

        [SerializeField]
        [Tooltip("Enable when the source sprite/art is drawn facing LEFT.\n\n" +
                 "Facing2D flips the object via localScale.x and assumes the art is authored " +
                 "facing RIGHT by default. If your sprite is drawn facing left, check this so the " +
                 "logical Right/Left directions still match what the player sees. " +
                 "Only the visual flip is inverted; DirSign / DirVector keep their logical meaning.")]
        private bool spriteFacedLeft;

        /// <summary>
        /// Current logical direction.
        /// </summary>
        public FacingDir Dir => dir;
        
        /// <summary>
        /// Opposite logical direction.
        /// </summary>
        public FacingDir OppositeDir => dir == FacingDir.Left ? FacingDir.Right : FacingDir.Left;
        
        /// <summary>
        /// Numeric direction sign.
        /// Right = 1, Left = -1.
        /// Useful for velocity, offsets, spawn direction, etc.
        /// </summary>
        public int DirSign => (int)dir;

        /// <summary>
        /// Returns true when the object is facing left.
        /// </summary>
        public bool IsLeft => dir == FacingDir.Left;

        /// <summary>
        /// Returns true when the object is facing right.
        /// </summary>
        public bool IsRight => dir == FacingDir.Right;

        /// <summary>
        /// Horizontal direction as a vector: (-1, 0) or (1, 0).
        /// </summary>
        public Vector2 DirVector => new(DirSign, 0f);

        private void Awake() {
            RefreshDir();
        }
        
        /// <summary>
        /// Sets a new logical direction and immediately applies it to the transform.
        /// Does nothing if the direction is already the same.
        /// </summary>
        public void SetDir(FacingDir newDir) {
            if (dir == newDir) {
                return;
            }

            dir = newDir;
            RefreshDir();
        }

        /// <summary>
        /// Flips the direction.
        /// </summary>
        public void FlipDir() {
            SetDir(OppositeDir);
        }

        /// <summary>
        /// Reapplies the current logical direction to the transform.
        /// Useful if something external modified the scale and you want to restore it.
        /// </summary>
        public void RefreshDir() {
            Vector3 scale = transform.localScale;
            float absX = Mathf.Abs(scale.x);

            if (absX < 0.0001f) {
                absX = 1f;
            }

            // Invert the flip when the art is authored facing left, so logical Right/Left
            // still match the visible orientation.
            int artSign = spriteFacedLeft ? -1 : 1;

            scale.x = absX * DirSign * artSign;
            transform.localScale = scale;
        }

        /// <summary>
        /// Sets direction to left.
        /// </summary>
        public void SetLeft() {
            SetDir(FacingDir.Left);
        }

        /// <summary>
        /// Sets direction to right.
        /// </summary>
        public void SetRight() {
            SetDir(FacingDir.Right);
        }

        /// <summary>
        /// Sets direction from an X value.
        /// Positive X -> Right.
        /// Negative X -> Left.
        /// Zero -> no change.
        /// </summary>
        public void SetByX(float x) {
            if (x > 0f) {
                SetRight();
            } else if (x < 0f) {
                SetLeft();
            }
        }

        /// <summary>
        /// Applies the current logical direction to Facing2D of the given instance. If the instance does
        /// not have Facing2D component, throws an exception.
        /// </summary>
        /// <param name="instance"></param>
        /// <exception cref="Exception"></exception>
        public void ApplyTo(GameObject instance) {
            var facing = instance.GetComponent<Facing2D>();
            if (facing == null) {
                throw new System.Exception($"Instance {instance} does not have Facing2D component.");
            }
            
            facing.SetDir(dir);
        }

        private void OnValidate() {
            RefreshDir();
        }
    }
}
