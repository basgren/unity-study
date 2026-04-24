using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.SpriteSheetAnimationImporter {
    /// <summary>
    /// Creates one AnimationClip per state row, containing all frames from that row
    /// in column order and animating <c>SpriteRenderer.m_Sprite</c>.
    /// </summary>
    public static class SpriteSheetAnimationClipBuilder {
        /// <summary>
        /// Generates or overwrites animation clips at <paramref name="outputFolder"/>.
        /// Returns a map of state name to clip asset for later controller wiring.
        /// </summary>
        public static Dictionary<string, AnimationClip> BuildClips(
            SpriteSheetImportSettings settings,
            string outputFolder) {
            var sourcePath = AssetDatabase.GetAssetPath(settings.sourceTexture);
            var assets = AssetDatabase.LoadAllAssetsAtPath(sourcePath);

            var spritesByName = new Dictionary<string, Sprite>(assets.Length);
            for (int i = 0; i < assets.Length; i++) {
                var sprite = assets[i] as Sprite;
                if (sprite != null) {
                    spritesByName[sprite.name] = sprite;
                }
            }

            var clips = new Dictionary<string, AnimationClip>(settings.stateNames.Count);

            for (int row = 0; row < settings.stateNames.Count; row++) {
                var stateName = settings.stateNames[row];
                var frames = CollectFrames(spritesByName, stateName, settings.columns);
                if (frames.Count == 0) {
                    Debug.LogWarning(
                        $"[SpriteSheetImporter] No sprites found for state '{stateName}'. Clip skipped.");
                    continue;
                }

                var clip = CreateOrReplaceClip(outputFolder, stateName, frames, settings.clipFps,
                    settings.loopClips);
                clips[stateName] = clip;
            }

            AssetDatabase.SaveAssets();
            return clips;
        }

        private static List<Sprite> CollectFrames(Dictionary<string, Sprite> spritesByName, string stateName,
            int columns) {
            var frames = new List<Sprite>(columns);
            for (int c = 0; c < columns; c++) {
                var name = $"{stateName}_{c:00}";
                if (spritesByName.TryGetValue(name, out var sprite) && sprite != null) {
                    frames.Add(sprite);
                }
            }

            return frames;
        }

        private static AnimationClip CreateOrReplaceClip(string outputFolder, string stateName,
            List<Sprite> frames, float fps, bool loop) {
            var assetPath = $"{outputFolder}/{stateName}.anim";

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            var isNew = clip == null;
            if (isNew) {
                clip = new AnimationClip();
            }

            clip.frameRate = fps;

            var binding = new EditorCurveBinding {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite"
            };

            var keyframes = new ObjectReferenceKeyframe[frames.Count];
            for (int i = 0; i < frames.Count; i++) {
                keyframes[i] = new ObjectReferenceKeyframe {
                    time = i / fps,
                    value = frames[i]
                };
            }

            // Clear any existing curves before writing - this overwrites prior frame data cleanly.
            var existingBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            for (int i = 0; i < existingBindings.Length; i++) {
                AnimationUtility.SetObjectReferenceCurve(clip, existingBindings[i], null);
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

            if (isNew) {
                AssetDatabase.CreateAsset(clip, assetPath);
            } else {
                EditorUtility.SetDirty(clip);
            }

            return clip;
        }
    }
}
