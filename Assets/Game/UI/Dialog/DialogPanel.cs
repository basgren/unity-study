using System.Collections;
using System.Collections.Generic;
using Game.Core.Bootstrap;
using Game.Core.Services.Dialog;
using Game.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI.Dialog {
    /// <summary>
    /// Dialog window UI. Subscribes to <see cref="DialogService"/> events and
    /// displays speaker name, dialog text (with typewriter reveal), and choice buttons.
    /// </summary>
    public class DialogPanel : TweenWindow {
        [Header("Text")]
        [SerializeField]
        private TextMeshProUGUI speakerText;

        [SerializeField]
        private TextMeshProUGUI bodyText;

        [Header("Choices")]
        [SerializeField]
        private Transform choiceContainer;

        [SerializeField]
        private DialogChoiceBtn choiceBtnPrefab;

        [SerializeField]
        private GameObject nextPageIndicator;

        [Header("Typewriter")]
        [SerializeField]
        private float charsPerSecond = 30f;

        private readonly List<DialogChoiceBtn> spawnedButtons = new();
        private bool showingChoices;
        private bool isTyping;
        private int selectedChoiceIndex;
        private Coroutine typewriterCoroutine;
        private InputAction selectAction;
        private InputAction cancelAction;
        private InputAction inventoryAction;
        private InputAction upAction;
        private InputAction downAction;

        protected override void Awake() {
            base.Awake();

            if (G.Dialog != null) {
                G.Dialog.StateChanged += HandleStateChanged;

                if (G.Dialog.IsActive) {
                    HandleStateChanged(G.Dialog.CurrentViewState);
                }
            }

            if (G.Input != null) {
                selectAction = G.Input.UI.Select;
                cancelAction = G.Input.UI.Cancel;
                inventoryAction = G.Input.UI.Inventory;
                upAction = G.Input.UI.Up;
                downAction = G.Input.UI.Down;
            }
        }

        private void OnDestroy() {
            if (G.Dialog != null) {
                G.Dialog.StateChanged -= HandleStateChanged;
            }
        }

        private void Update() {
            if (G.Dialog == null || !G.Dialog.IsActive) {
                return;
            }

            if (showingChoices) {
                HandleChoiceInput();
                return;
            }

            if (WasPressedThisFrame(selectAction) ||
                WasPressedThisFrame(cancelAction) ||
                WasPressedThisFrame(inventoryAction)) {
                if (isTyping) {
                    CompleteTypewriter();
                } else {
                    G.Dialog.Advance();
                }
            }
        }

        private void HandleChoiceInput() {
            if (WasPressedThisFrame(upAction)) {
                ChangeSelection(-1);
            } else if (WasPressedThisFrame(downAction)) {
                ChangeSelection(1);
            } else if (WasPressedThisFrame(selectAction)) {
                OnChoicePicked(selectedChoiceIndex);
            }
        }

        private bool WasPressedThisFrame(InputAction action) {
            return action != null && action.WasPressedThisFrame();
        }

        private void ChangeSelection(int delta) {
            if (spawnedButtons.Count == 0) {
                return;
            }

            selectedChoiceIndex = (selectedChoiceIndex + delta + spawnedButtons.Count) % spawnedButtons.Count;
            UpdateSelectionVisuals();
        }

        private void UpdateSelectionVisuals() {
            for (int i = 0; i < spawnedButtons.Count; i++) {
                spawnedButtons[i].SetSelected(i == selectedChoiceIndex);
            }
        }

        private void HandleStateChanged(DialogViewState state) {
            switch (state.Mode) {
                case DialogViewMode.Line:
                    ShowLine(state);
                    break;

                case DialogViewMode.Choices:
                    ShowChoices(state);
                    break;

                case DialogViewMode.Hidden:
                    HandleDialogEnded();
                    break;
            }
        }

        private void ShowLine(DialogViewState state) {
            speakerText.text = state.Speaker ?? "";
            showingChoices = false;

            ClearChoiceButtons();
            StartTypewriter(state.Text ?? "");
        }

        private void ShowChoices(DialogViewState state) {
            speakerText.text = state.Speaker ?? "";
            bodyText.text = state.Text ?? "";
            bodyText.ForceMeshUpdate();
            bodyText.maxVisibleCharacters = bodyText.textInfo.characterCount;

            showingChoices = true;
            StopTypewriter();

            if (nextPageIndicator != null) {
                nextPageIndicator.SetActive(false);
            }

            ClearChoiceButtons();

            var top = 0f;
            var choices = state.Choices;

            if (choices == null) {
                return;
            }

            for (int i = 0; i < choices.Count; i++) {
                var btn = Instantiate(choiceBtnPrefab, choiceContainer);
                btn.Setup(i, choices[i].text, OnChoicePicked);
                var rectTrans = btn.GetComponent<RectTransform>();
                btn.transform.localPosition = new Vector3(0f, top, 0f);
                top -= rectTrans.rect.height;
                spawnedButtons.Add(btn);
            }

            selectedChoiceIndex = 0;
            UpdateSelectionVisuals();
        }

        private void HandleDialogEnded() {
            StopTypewriter();
            showingChoices = false;
            ClearChoiceButtons();
            SetNextPageVisible(false);
            G.Menu.CloseTopWindow();
        }

        private void OnChoicePicked(int index) {
            G.Dialog.PickChoice(index);
        }

        private void ClearChoiceButtons() {
            for (int i = 0; i < spawnedButtons.Count; i++) {
                Destroy(spawnedButtons[i].gameObject);
            }

            spawnedButtons.Clear();
        }

        // --- Typewriter ---

        private void StartTypewriter(string text) {
            StopTypewriter();

            bodyText.text = text;
            bodyText.ForceMeshUpdate();

            var totalChars = bodyText.textInfo.characterCount;

            if (totalChars == 0 || charsPerSecond <= 0) {
                bodyText.maxVisibleCharacters = totalChars;
                isTyping = false;
                NotifyCurrentLineFullyRevealed();

                return;
            }

            bodyText.maxVisibleCharacters = 0;
            isTyping = true;

            if (nextPageIndicator != null) {
                nextPageIndicator.SetActive(false);
            }

            typewriterCoroutine = StartCoroutine(TypewriterRoutine(totalChars));
        }

        private IEnumerator TypewriterRoutine(int totalChars) {
            var delay = 1f / charsPerSecond;
            int revealed = 0;
            
            SetNextPageVisible(false);

            while (revealed < totalChars) {
                revealed++;
                bodyText.maxVisibleCharacters = revealed;
                yield return new WaitForSecondsRealtime(delay);
            }

            CompleteTypewriter();
        }

        private void CompleteTypewriter() {
            StopTypewriter();

            bodyText.maxVisibleCharacters = bodyText.textInfo.characterCount;
            NotifyCurrentLineFullyRevealed();
        }

        private void StopTypewriter() {
            if (typewriterCoroutine != null) {
                StopCoroutine(typewriterCoroutine);
                typewriterCoroutine = null;
            }

            isTyping = false;
        }

        private void SetNextPageVisible(bool isVisible) {
            if (nextPageIndicator != null) {
                nextPageIndicator.SetActive(isVisible);
            }
        }

        private void NotifyCurrentLineFullyRevealed() {
            if (G.Dialog == null) {
                SetNextPageVisible(false);
                return;
            }

            G.Dialog.NotifyCurrentLineFullyRevealed();

            if (G.Dialog.IsActive && !showingChoices && !isTyping) {
                SetNextPageVisible(true);
            }
        }

    }
}
