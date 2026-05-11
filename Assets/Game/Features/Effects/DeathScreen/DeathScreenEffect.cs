using System;
using System.Collections;
using Game.Core.Bootstrap;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.U2D;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Game.Features.Effects.DeathScreen {
    /// <summary>
    /// Orchestrates the hero death visual sequence:
    ///   1. Gameplay slowdown (Time.timeScale).
    ///   2. Fast desaturation via URP ColorAdjustments (saturation goes to -100).
    ///   3. Stylized iris / vignette closing around the hero (follows hero every frame).
    ///   4. Final fade-to-black hand-off to respawn.
    ///
    /// The timeline runs on <see cref="Time.unscaledDeltaTime"/>, so the effect duration
    /// is unaffected by the gameplay slowdown. Canvas, overlay image, and URP volume are
    /// built at runtime to avoid extra prefab wiring.
    /// </summary>
    public class DeathScreenEffect : MonoBehaviour {
        private const string IrisShaderName = "Game/UI/DeathScreenIris";
        // Well above normal HUD canvases so the iris draws above everything.
        private const int CanvasSortingOrder = 32000;

        // Tuning values come from MainConfig at runtime via Init(); this service is spawned
        // dynamically by GInit, so serialized fields are not used here (see AGENTS.md).
        // A default instance keeps the component usable in edge cases (tests, early calls,
        // or scenes loaded without GInit).
        private DeathScreenSettings settings = new DeathScreenSettings();

        private static readonly int CenterId     = Shader.PropertyToID("_Center");
        private static readonly int RadiusId     = Shader.PropertyToID("_Radius");
        private static readonly int AspectId     = Shader.PropertyToID("_Aspect");
        private static readonly int DarknessId   = Shader.PropertyToID("_Darkness");
        private static readonly int FadeAllId    = Shader.PropertyToID("_FadeAll");
        private static readonly int EdgeWidthId  = Shader.PropertyToID("_EdgeWidth");
        private static readonly int ResolutionId = Shader.PropertyToID("_Resolution");
        private static readonly int UseDitherId  = Shader.PropertyToID("_UseDither");

        private Canvas canvas;
        private RawImage irisImage;
        private Material irisMat;
        private Volume volume;
        private ColorAdjustments colorAdjust;
        private Coroutine running;
        private bool subscribedToSceneTravel;

        public bool IsPlaying => running != null;
        public float TotalRespawnDelay => settings.TotalRespawnDelay;

        private void Awake() {
            BuildCanvas();
            BuildVolume();
            HideImmediately();
        }

        /// <summary>
        /// Applies tuning values from <c>MainConfig</c>. Called by <c>GInit</c> after the
        /// config is loaded. Safe to call again to hot-swap settings.
        /// </summary>
        public void Init(DeathScreenSettings configSettings) {
            if (configSettings != null) {
                settings = configSettings;
            }
        }

        private void OnEnable() {
            TrySubscribeSceneTravel();
        }

        private void Start() {
            TrySubscribeSceneTravel();
        }

        private void OnDisable() {
            if (subscribedToSceneTravel && G.SceneTravel != null) {
                G.SceneTravel.AfterTransition -= OnAfterSceneTransition;
                subscribedToSceneTravel = false;
            }
        }

        private void TrySubscribeSceneTravel() {
            if (subscribedToSceneTravel) {
                return;
            }

            if (G.SceneTravel == null) {
                return;
            }

            G.SceneTravel.AfterTransition += OnAfterSceneTransition;
            subscribedToSceneTravel = true;
        }

        private void OnAfterSceneTransition(Scene from, Scene to) {
            // After any scene load, clear the overlay so the fresh scene is visible.
            ResetVisuals();
        }

        /// <summary>
        /// Runs the full death sequence. <paramref name="heroTransform"/> is sampled each
        /// frame so the iris follows the hero (important for mid-air deaths where the body
        /// continues to fall). <paramref name="onFadeComplete"/> is invoked once the screen
        /// has reached full black and <c>Time.timeScale</c> has been restored to 1.
        /// </summary>
        public void Play(Transform heroTransform, Action onFadeComplete = null) {
            if (running != null) {
                StopCoroutine(running);
            }

            running = StartCoroutine(Run(heroTransform, onFadeComplete));
        }

        /// <summary>
        /// Immediately restores time scale and hides all visuals. Safe to call at any time.
        /// Automatically called after every scene transition.
        /// </summary>
        public void ResetVisuals() {
            if (running != null) {
                StopCoroutine(running);
                running = null;
            }

            Time.timeScale = 1f;

            if (colorAdjust != null) {
                colorAdjust.saturation.overrideState = false;
                colorAdjust.saturation.value = 0f;
            }

            HideImmediately();
        }

        private void HideImmediately() {
            if (irisMat != null) {
                irisMat.SetFloat(RadiusId, 2f);
                irisMat.SetFloat(FadeAllId, 0f);
            }

            if (irisImage != null) {
                irisImage.enabled = false;
            }
        }

        private IEnumerator Run(Transform hero, Action onFadeComplete) {
            Time.timeScale = settings.SlowTimeScale;

            EnableCameraPostProcessing();

            // Graceful degrade: if setup failed, still keep the caller's timing contract.
            if (irisMat == null) {
                yield return new WaitForSecondsRealtime(settings.TotalRespawnDelay);
                Time.timeScale = 1f;
                running = null;
                onFadeComplete?.Invoke();
                yield break;
            }

            if (colorAdjust != null) {
                colorAdjust.saturation.overrideState = true;
            }

            irisMat.SetFloat(DarknessId, settings.VignetteDarkness);
            irisMat.SetFloat(EdgeWidthId, settings.EdgeWidth);
            irisMat.SetFloat(UseDitherId, settings.UseDither ? 1f : 0f);
            irisMat.SetVector(ResolutionId, ResolveVirtualResolution());
            irisMat.SetFloat(RadiusId, settings.InitialRadius);
            irisMat.SetFloat(FadeAllId, 0f);
            irisImage.enabled = true;

            var t = 0f;

            while (t < settings.TotalRespawnDelay) {
                t += Time.unscaledDeltaTime;

                UpdateShaderCenter(hero);

                // Desaturation (subtle, fast).
                if (colorAdjust != null) {
                    var satK = Mathf.Clamp01(t / Mathf.Max(0.0001f, settings.DesaturateDuration));
                    colorAdjust.saturation.value = Mathf.Lerp(0f, -100f, satK);
                }

                // Iris shrink (delayed start, ease-in).
                var irisT = Mathf.Clamp01((t - settings.IrisStartDelay) / Mathf.Max(0.0001f, settings.IrisShrinkDuration));
                var eased = irisT * irisT;
                irisMat.SetFloat(RadiusId, Mathf.Lerp(settings.InitialRadius, settings.FinalRadius, eased));

                // Final fade to black (fills the remaining iris window with darkness).
                var fadeStart = settings.TotalRespawnDelay - settings.FadeToBlackDuration;
                var fadeK = t >= fadeStart
                    ? Mathf.Clamp01((t - fadeStart) / Mathf.Max(0.0001f, settings.FadeToBlackDuration))
                    : 0f;
                irisMat.SetFloat(FadeAllId, fadeK);

                yield return null;
            }

            Time.timeScale = 1f;
            running = null;
            onFadeComplete?.Invoke();
        }

        private void UpdateShaderCenter(Transform hero) {
            var cam = UnityEngine.Camera.main;
            if (hero != null && cam != null) {
                var vp = cam.WorldToViewportPoint(hero.position + new Vector3(0, 0.3f, 0));
                irisMat.SetVector(CenterId, new Vector4(vp.x, vp.y, 0, 0));
            }

            var aspect = Screen.width > 0 && Screen.height > 0
                ? (float)Screen.width / Screen.height
                : 16f / 9f;
            irisMat.SetFloat(AspectId, aspect);

            // Resolution can change with window resize and PixelPerfectCamera reacts to it,
            // so refresh each frame to keep dither blocks the right size.
            irisMat.SetVector(ResolutionId, ResolveVirtualResolution());
        }

        private void BuildCanvas() {
            var canvasGo = new GameObject("DeathScreen_Canvas");
            canvasGo.transform.SetParent(transform, worldPositionStays: false);

            canvas = canvasGo.AddComponent<Canvas>();
            // ScreenSpaceOverlay so the iris is rendered AFTER all URP passes, including
            // sprites, 2D lights, and post-processing. ScreenSpaceCamera sits inside the
            // URP 2D render flow and can be occluded by later sprite/light passes.
            // To keep the pixel-art alignment, the shader still snaps to a virtual grid;
            // the controller feeds it Screen.size / PixelPerfectCamera.pixelRatio each
            // frame so dither blocks match the sprite pixel size on-screen.
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(480, 270);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            var imageGo = new GameObject("DeathScreen_Iris");
            imageGo.transform.SetParent(canvasGo.transform, worldPositionStays: false);

            var rt = imageGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            irisImage = imageGo.AddComponent<RawImage>();
            irisImage.raycastTarget = false;
            irisImage.maskable = false;

            var shader = Shader.Find(IrisShaderName);
            if (shader == null) {
                Debug.LogError(
                    $"{nameof(DeathScreenEffect)}: shader '{IrisShaderName}' not found. " +
                    "For builds, add it to Always Included Shaders (Project Settings > Graphics) " +
                    "or reference it via a material asset.",
                    this);
                return;
            }

            irisMat = new Material(shader) { name = "DeathScreenIris (runtime)" };
            irisImage.material = irisMat;
        }

        // Derives the virtual-pixel resolution of the full screen for shader dithering/snapping.
        //
        // The overlay canvas draws at raw screen resolution, so we need to tell the shader
        // how many "virtual pixels" span the window so its dither/snap grid matches the
        // sprite pixel size. Algorithm:
        //   1. Find the integer pixel scale the PixelPerfectCamera is using on-screen
        //      (height / refResolutionY, rounded to an integer >= 1).
        //   2. virtualSize = Screen.size / scale — a grid where each cell is one sprite pixel.
        //
        // This is more reliable than <c>PixelPerfectCamera.pixelRatio</c>, which can report
        // 1 in the Editor / non-standard Game-view sizes, collapsing the dither to screen
        // pixels.
        private Vector4 ResolveVirtualResolution() {
            var refW = 480;
            var refH = 270;

            var cam = UnityEngine.Camera.main;
            if (cam != null) {
                var pp = cam.GetComponent<PixelPerfectCamera>();
                if (pp != null) {
                    refW = Mathf.Max(1, pp.refResolutionX);
                    refH = Mathf.Max(1, pp.refResolutionY);
                }
            }

            var scale = Mathf.Max(1, Mathf.RoundToInt(Screen.height / (float)refH));
            var w = Mathf.Max(1f, Screen.width  / (float)scale);
            var h = Mathf.Max(1f, Screen.height / (float)scale);
            return new Vector4(w, h, 0, 0);
        }

        private void BuildVolume() {
            var volGo = new GameObject("DeathScreen_Volume");
            volGo.transform.SetParent(transform, worldPositionStays: false);

            volume = volGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000;
            volume.weight = 1f;
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

            colorAdjust = volume.profile.Add<ColorAdjustments>(overrides: true);
            colorAdjust.active = true;
            colorAdjust.saturation.overrideState = false;
            colorAdjust.saturation.value = 0f;
        }

        private void EnableCameraPostProcessing() {
            var cam = UnityEngine.Camera.main;
            if (cam == null) {
                return;
            }

            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null && !data.renderPostProcessing) {
                data.renderPostProcessing = true;
            }
        }
    }
}