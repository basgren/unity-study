using UnityEngine;

namespace Components {
    [ExecuteAlways]
    [RequireComponent(typeof(BoxCollider2D))]
    public class Collider2DAutoSize : MonoBehaviour {
        [Header("Source")]
        [SerializeField,
         Tooltip("SpriteRenderer used as a reference for collider size (usually the 'SpikesVisual' child).")]
        private SpriteRenderer sourceRenderer;

        [Header("Shape")]
        [SerializeField, Tooltip("Horizontal padding from visual bounds on each side, in world units.")]
        [Min(0f)]
        private float horizontalPadding = 0.05f;

        [SerializeField, Tooltip("Fraction of the visual height used for the collider (0.5 = bottom half).")]
        [Range(0.1f, 1f)]
        private float heightFraction = 0.5f;

        [SerializeField, Tooltip("If true, the collider is aligned to the bottom of the visual bounds.")]
        private bool alignToBottom = true;

        private BoxCollider2D boxCollider;

#if UNITY_EDITOR
        // Cache to detect changes in the editor
        private Sprite lastSprite;
        private Vector2 lastVisualSize;
        private SpriteDrawMode lastDrawMode;
        private Vector3 lastLocalPosition;
        private Vector3 lastLocalScale;
#endif

        private void Awake() {
            // In play mode we only need to compute collider once.
            if (Application.isPlaying) {
                Init();
                ResolveSourceRendererIfNeeded();
                ForceUpdateCollider();
            }
        }

        private void Reset() {
            Init();

            boxCollider.isTrigger = true;

            ResolveSourceRendererIfNeeded();
            ForceUpdateCollider();
        }

        private void OnValidate() {
            Init();
            ResolveSourceRendererIfNeeded();
            ForceUpdateCollider();
        }

        private void Init() {
            if (boxCollider == null) {
                boxCollider = GetComponent<BoxCollider2D>();
            }
        }

        private void ResolveSourceRendererIfNeeded() {
            if (sourceRenderer == null) {
                sourceRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

#if UNITY_EDITOR
        private void Update() {
            if (sourceRenderer == null || boxCollider == null) {
                return;
            }

            if (HasVisualChanged()) {
                ForceUpdateCollider();
            }
        }


        private bool HasVisualChanged() {
            if (sourceRenderer == null) {
                return false;
            }

            Transform t = sourceRenderer.transform;
            Sprite currentSprite = sourceRenderer.sprite;
            SpriteDrawMode currentMode = sourceRenderer.drawMode;

            // Visual size BEFORE taking child transform into account
            Vector2 baseSize;
            if (currentMode == SpriteDrawMode.Simple) {
                if (currentSprite == null) {
                    baseSize = Vector2.zero;
                } else {
                    baseSize = currentSprite.bounds.size;
                }
            } else {
                baseSize = sourceRenderer.size;
            }

            // Apply local scale of the visual transform
            Vector2 currentSize = new Vector2(
                baseSize.x * Mathf.Abs(t.localScale.x),
                baseSize.y * Mathf.Abs(t.localScale.y)
            );

            Vector3 currentLocalPos = t.localPosition;
            Vector3 currentLocalScale = t.localScale;

            bool changed = lastSprite != currentSprite;

            if (lastDrawMode != currentMode) {
                changed = true;
            }

            if (lastVisualSize != currentSize) {
                changed = true;
            }

            if (lastLocalPosition != currentLocalPos) {
                changed = true;
            }

            if (lastLocalScale != currentLocalScale) {
                changed = true;
            }

            if (changed) {
                lastSprite = currentSprite;
                lastDrawMode = currentMode;
                lastVisualSize = currentSize;
                lastLocalPosition = currentLocalPos;
                lastLocalScale = currentLocalScale;
            }

            return changed;
        }
#endif

        private void ForceUpdateCollider() {
            if (sourceRenderer == null || boxCollider == null) {
                return;
            }

            if (sourceRenderer.sprite == null) {
                return;
            }

            Transform t = sourceRenderer.transform;

            // 1. Базовый размер (без учёта transform)
            Vector2 spriteSize;

            if (sourceRenderer.drawMode == SpriteDrawMode.Simple) {
                spriteSize = sourceRenderer.sprite.bounds.size;
            } else {
                spriteSize = sourceRenderer.size;
            }

            Vector2 colliderSize = new Vector2(
                Mathf.Max(0f, spriteSize.x - 2f * horizontalPadding),
                spriteSize.y * heightFraction
            );

            float colliderCenterY = alignToBottom
                ? colliderSize.y * 0.5f
                : spriteSize.y - colliderSize.y * 0.5f;

            float colliderCenterX = spriteSize.x * 0.5f;

            boxCollider.size = new Vector2(colliderSize.x, colliderSize.y);
            boxCollider.offset = new Vector2(colliderCenterX, colliderCenterY);
        }
    }
}
