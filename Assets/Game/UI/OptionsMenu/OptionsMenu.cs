using System;
using Core.Services;
using Core.UI;
using Game.UI.Widgets;
using UnityEngine;

namespace Game.UI.OptionsMenu {
    public class OptionsMenu : AnimatedWindow {
        [SerializeField] private LabeledSlider musicSlider;
        [SerializeField] private LabeledSlider sfxSlider;

        public override void Open(GameObject selected = null) {
            LoadSettings();
            base.Open(selected);
        }

        public override void Close(Action afterClosed = null) {
            SaveSettings();
            base.Close(afterClosed);
        }

        private void OnEnable() {
            if (musicSlider != null) {
                musicSlider.OnValueChanged.AddListener(OnMusicChanged);
            }

            if (sfxSlider != null) {
                sfxSlider.OnValueChanged.AddListener(OnSfxChanged);
            }
        }

        private void OnDisable() {
            if (musicSlider != null) {
                musicSlider.OnValueChanged.RemoveListener(OnMusicChanged);
            }

            if (sfxSlider != null) {
                sfxSlider.OnValueChanged.RemoveListener(OnSfxChanged);
            }
        }

        public void OnBackClick() {
            G.Menu.CloseTopWindow();
        }

        private void LoadSettings() {
            if (G.Settings == null) {
                return;
            }

            var settings = G.Settings.Current;

            if (musicSlider != null) {
                musicSlider.Value = settings.MusicVolume;
            }

            if (sfxSlider != null) {
                sfxSlider.Value = settings.SfxVolume;
            }
        }

        private void SaveSettings() {
            if (G.Settings == null) {
                return;
            }

            var settings = G.Settings.Current;
            settings.MusicVolume = musicSlider != null ? musicSlider.Value : settings.MusicVolume;
            settings.SfxVolume = sfxSlider != null ? sfxSlider.Value : settings.SfxVolume;
            G.Settings.Save();
        }

        private void OnMusicChanged(int value) {
            if (G.Settings == null) {
                return;
            }

            G.Settings.Current.MusicVolume = value;
            G.Settings.ApplyVolume();
        }

        private void OnSfxChanged(int value) {
            if (G.Settings == null) {
                return;
            }

            G.Settings.Current.SfxVolume = value;
            G.Settings.ApplyVolume();
        }
    }
}
