using UnityEditor;
using UnityEngine;

namespace Core.Audio.Editor {
    /// <summary>
    /// Adds Play / Stop buttons to the top of every <see cref="AudioCue"/> inspector
    /// so designers can preview a cue in Edit mode, routed through its configured
    /// <see cref="AudioCue.MixerGroup"/>. The mixer chain is honored, so reverb and
    /// other effects sound the same as in-game.
    ///
    /// Cleanup is tracked through static state because there is only ever one active
    /// preview. Anything that could orphan the temporary preview object — clip ending
    /// naturally, the user navigating away from the asset, entering / exiting Play
    /// mode, or an assembly reload — destroys the object and unhooks events.
    /// </summary>
    [CustomEditor(typeof(AudioCue))]
    public class AudioCueEditor : UnityEditor.Editor {
        private const string PreviewObjectName = "__AudioCuePreview";

        // Only one preview can be active. Tracked statically so the editor instance
        // that started it does not have to be the one that cleans it up — domain
        // reloads, asset switches, and play-mode changes all share this state.
        private static GameObject previewObject;
        private static AudioSource previewSource;
        private static AudioCue previewCue;
        private static bool eventsHooked;

        public override void OnInspectorGUI() {
            AudioCue cue = (AudioCue)target;

            DrawPreviewBar(cue);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        private void DrawPreviewBar(AudioCue cue) {
            bool isPlayingThis = previewCue == cue && previewSource != null && previewSource.isPlaying;
            bool isPreviewing = previewObject != null;

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(!cue.HasClips())) {
                if (GUILayout.Button(isPlayingThis ? "Replay" : "Play", GUILayout.Height(24))) {
                    PlayPreview(cue);
                }
            }

            using (new EditorGUI.DisabledScope(!isPreviewing)) {
                if (GUILayout.Button("Stop", GUILayout.Height(24))) {
                    StopPreview();
                }
            }

            EditorGUILayout.EndHorizontal();

            if (isPlayingThis && previewSource != null && previewSource.clip != null) {
                AudioClip clip = previewSource.clip;
                float t = clip.length > 0f ? previewSource.time / clip.length : 0f;
                Rect r = EditorGUILayout.GetControlRect();
                EditorGUI.ProgressBar(r, t, clip.name);
                Repaint();
            }
        }

        private static void PlayPreview(AudioCue cue) {
            // Always tear down whatever was playing first — that covers replays of the
            // same cue as well as switching from a different one.
            StopPreview();

            AudioClip clip = cue.PickClip();
            if (clip == null) {
                Debug.LogWarning("[AudioCueEditor] Cue has no clip to preview.", cue);
                return;
            }

            previewObject = new GameObject(PreviewObjectName);
            // HideAndDontSave: hidden from hierarchy, never written to scene/build,
            // and Unity destroys it on domain reload — so we get cleanup for free in
            // the cases the explicit hooks below do not catch.
            previewObject.hideFlags = HideFlags.HideAndDontSave;

            previewSource = previewObject.AddComponent<AudioSource>();
            previewSource.clip = clip;
            previewSource.volume = cue.Volume;
            previewSource.pitch = cue.PickPitch();
            previewSource.outputAudioMixerGroup = cue.MixerGroup;
            previewSource.spatialBlend = 0f;
            previewSource.playOnAwake = false;
            previewSource.loop = false;
            previewSource.Play();

            previewCue = cue;
            HookEvents();
        }

        private static void StopPreview() {
            UnhookEvents();

            if (previewObject != null) {
                Object.DestroyImmediate(previewObject);
            }
            previewObject = null;
            previewSource = null;
            previewCue = null;
        }

        private void OnDisable() {
            // Inspector closed or asset deselected — if it was previewing this cue,
            // tear down. Comparing target catches the case where multiple cue
            // inspectors are open and only one of them is closing.
            if (target == previewCue) {
                StopPreview();
            }
        }

        // -------- Event plumbing --------

        private static void HookEvents() {
            if (eventsHooked) {
                return;
            }
            eventsHooked = true;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
        }

        private static void UnhookEvents() {
            if (!eventsHooked) {
                return;
            }
            eventsHooked = false;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
        }

        private static void OnEditorUpdate() {
            // The preview object may have been destroyed externally (scene reload,
            // user wiping with DestroyImmediate from a script, etc.).
            if (previewSource == null) {
                StopPreview();
                return;
            }
            if (!previewSource.isPlaying) {
                StopPreview();
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange state) {
            // Any transition kills the preview — leaving a temp AudioSource around
            // while entering Play mode would compete with the real audio system.
            StopPreview();
        }
    }
}
