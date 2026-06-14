using Core.Audio;
using Game.Core.Bootstrap;
using UnityEngine;

namespace Game.Core.Audio {
    /// <summary>
    /// A level volume that overrides the scene's music while the player is inside it. On enter the
    /// zone pushes <see cref="zoneMusic"/> as the active track; on exit it reverts to the next zone
    /// the player still occupies, the scene's default music, or silence (a fade-out) when the scene
    /// has no default. Leave <see cref="zoneMusic"/> empty to make a deliberate "silence" zone.
    ///
    /// Place on a GameObject with a trigger <see cref="Collider2D"/>. The override is released on
    /// exit and on disable/teardown, so the zone never leaves a dangling track behind.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class MusicZone : MonoBehaviour {
        private const string PlayerTag = "Player";

        [SerializeField]
        [Tooltip("Track to play while the player is inside this zone. Leave empty for a silence zone.")]
        private AudioCue zoneMusic;

        // Player colliders currently inside. Ref-counted so a player with more than one tagged
        // collider (or a brief double-overlap) cannot cause a premature revert.
        private int playerInsideCount;
        private bool active;

        private void Reset() {
            // A music zone only makes sense as a trigger; set it up for designers on add.
            var col = GetComponent<Collider2D>();
            if (col != null) {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other) {
            if (!other.CompareTag(PlayerTag)) {
                return;
            }

            playerInsideCount++;
            if (playerInsideCount == 1) {
                Activate();
            }
        }

        private void OnTriggerExit2D(Collider2D other) {
            if (!other.CompareTag(PlayerTag)) {
                return;
            }

            playerInsideCount = Mathf.Max(0, playerInsideCount - 1);
            if (playerInsideCount == 0) {
                Deactivate();
            }
        }

        private void OnDisable() {
            playerInsideCount = 0;
            Deactivate();
        }

        private void Activate() {
            if (active || G.Audio == null) {
                return;
            }

            active = true;
            G.Audio.PushMusicOverride(zoneMusic);
        }

        private void Deactivate() {
            if (!active) {
                return;
            }

            active = false;

            // Don't revert while this zone is being torn down by a scene change — the player has not
            // really left it (its collider is just being destroyed). Reverting would briefly switch
            // to the old scene's default and restart a track the destination scene wants to keep
            // playing. The destination's SetLevelMusic resets the override stack instead. Covers both
            // the central loader and any direct scene unload.
            bool sceneChanging = (G.SceneTravel != null && G.SceneTravel.IsTransitioning)
                                 || !gameObject.scene.isLoaded;
            if (sceneChanging) {
                return;
            }

            if (G.Audio != null) {
                G.Audio.RemoveMusicOverride(zoneMusic);
            }
        }
    }
}
