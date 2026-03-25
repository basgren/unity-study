using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI.Widgets {
    /// <summary>
    /// Integer slider widget with a live-updating value label.
    /// Set min/max on the Slider component; this script enforces whole numbers.
    /// </summary>
    public class LabeledSlider : MonoBehaviour {
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_Text valueText;

        [SerializeField] private UnityEvent<int> onValueChanged;

        public int Value {
            get => Mathf.RoundToInt(slider.value);
            set {
                slider.value = value;
                UpdateValueText(value);
            }
        }

        public UnityEvent<int> OnValueChanged => onValueChanged;

        private void Awake() {
            if (slider != null) {
                slider.wholeNumbers = true;
            }
        }

        private void OnEnable() {
            if (slider != null) {
                slider.onValueChanged.AddListener(OnSliderChanged);
            }
        }

        private void OnDisable() {
            if (slider != null) {
                slider.onValueChanged.RemoveListener(OnSliderChanged);
            }
        }

        private void OnSliderChanged(float rawValue) {
            var intValue = Mathf.RoundToInt(rawValue);
            UpdateValueText(intValue);
            onValueChanged?.Invoke(intValue);
        }

        private void UpdateValueText(int value) {
            if (valueText != null) {
                valueText.text = value.ToString();
            }
        }
    }
}
