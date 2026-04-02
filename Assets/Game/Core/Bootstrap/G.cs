using Game.Configs;
using Game.Core.Audio;
using Game.Core.Services;
using Game.Core.Services.Dialog;
using Game.Core.Services.Input;
using Game.Core.Services.Locale;
using Game.Core.Services.Scene;
using Game.Core.Services.Tween;

namespace Game.Core.Bootstrap {
    /// <summary>
    /// Global class for system services. To make it available, add empty game object "System" to
    /// the scene and add SystemInitializer component to it.
    /// For now, when the same is simple, it's enough to use it like global class with constants.
    /// If game gets bigger, it should be reconsidered and migrated to ServiceLocator or DI.
    /// </summary>
    public static class G {
        public static SpawnerService Spawner { get; internal set; }
        public static InputService Input { get; internal set; }
        public static ScreenService Screen { get; internal set; }
        public static GameManager Game { get; internal set; }
        public static StateMachineService StateMachines { get; internal set; }
        public static IAudioService Audio { get; internal set; }
        public static SettingsService Settings { get; internal set; }
        public static MainConfig Config { get; internal set; }
        public static HeroService Hero { get; internal set; }
        public static HudService Hud { get; internal set; }
        public static MenuManager Menu { get; internal set; }
        public static TweenService Tween { get; internal set; }
        public static LocaleService Locale { get; internal set; }
        public static DialogService Dialog { get; internal set; }
    }
}
