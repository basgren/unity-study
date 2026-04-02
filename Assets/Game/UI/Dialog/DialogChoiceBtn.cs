using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Dialog {
    /// <summary>
    /// Controls a single dialog choice button. Created dynamically by <see cref="DialogPanel"/>.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class DialogChoiceBtn : MonoBehaviour {
        [SerializeField]
        private TextMeshProUGUI label;
        
        [SerializeField]
        private TextMeshProUGUI selectionMarker;

        private Button button;
        private int choiceIndex;
        private Action<int> onClick;

        private void Awake() {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnClick);

            if (selectionMarker != null) {
                selectionMarker.gameObject.SetActive(false);
            }
        }

        public void Setup(int index, string text, Action<int> callback) {
            choiceIndex = index;
            onClick = callback;
            label.text = text;
        }

        public void SetSelected(bool selected) {
            if (selectionMarker != null) {
                selectionMarker.gameObject.SetActive(selected);
            }
        }

        public void OnClick() {
            onClick?.Invoke(choiceIndex);
        }

        private void OnDestroy() {
            button.onClick.RemoveListener(OnClick);
        }
    }
}
