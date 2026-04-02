using System;
using System.Collections.Generic;
using Core.Audio;
using Game.Core.Bootstrap;
using Game.Core.Models.Dialog;
using Game.Features.Characters.Hero;
using UnityEngine;

namespace Game.Core.Services.Dialog {
    public enum DialogViewMode {
        Hidden,
        Line,
        Choices
    }

    public readonly struct DialogViewState {
        public readonly DialogViewMode Mode;
        public readonly string Speaker;
        public readonly string Text;
        public readonly IReadOnlyList<DialogChoice> Choices;

        public DialogViewState(DialogViewMode mode, string speaker, string text, IReadOnlyList<DialogChoice> choices) {
            Mode = mode;
            Speaker = speaker;
            Text = text;
            Choices = choices;
        }
    }

    /// <summary>
    /// Drives the runtime state of an active dialog conversation.
    /// Loads dialog definitions, manages node traversal, evaluates conditions,
    /// executes actions, and emits events for the UI layer.
    /// </summary>
    public sealed class DialogService : MonoBehaviour {
        public event Action<DialogViewState> StateChanged;

        private DialogDef currentDialog;
        private DialogNode currentNode;
        private int currentLineIndex;
        private List<DialogChoice> visibleChoices;
        private DialogViewState currentViewState;
        private bool isCurrentLineFullyRevealed;
        private bool showingChoices;
        private IAudioLoopHandle activeSpeechSound;

        public bool IsActive => currentDialog != null;
        public DialogViewState CurrentViewState => currentViewState;

        public void StartDialog(string dialogId) {
            if (IsActive) {
                Debug.LogWarning($"DialogService: dialog already active, ending current before starting '{dialogId}'.");
                EndDialog();
            }

            currentDialog = DialogLoader.Load(dialogId);

            if (currentDialog == null) {
                return;
            }

            G.Menu.OpenMenu(G.Config.DialogPanel);
            EnterNode(currentDialog.entryNodeId);
        }

        public void Advance() {
            if (!IsActive || showingChoices || !isCurrentLineFullyRevealed) {
                return;
            }

            if (currentNode != null && currentNode.lines != null && currentLineIndex < currentNode.lines.Length - 1) {
                currentLineIndex++;
                EmitCurrentLine();
                return;
            }

            EndDialog();
        }

        public void NotifyCurrentLineFullyRevealed() {
            if (!IsActive || showingChoices || isCurrentLineFullyRevealed) {
                return;
            }

            isCurrentLineFullyRevealed = true;

            if (IsLastLineInCurrentNode()) {
                TryShowChoices();
            }
        }

        public void PickChoice(int choiceIndex) {
            if (!IsActive || visibleChoices == null) {
                return;
            }

            if (choiceIndex < 0 || choiceIndex >= visibleChoices.Count) {
                return;
            }

            var choice = visibleChoices[choiceIndex];
            ExecuteActions(choice.actions);

            if (!string.IsNullOrEmpty(choice.nextNodeId)) {
                EnterNode(choice.nextNodeId);
            } else {
                EndDialog();
            }
        }

        public void EndDialog() {
            StopSpeechSound();
            currentDialog = null;
            currentNode = null;
            currentLineIndex = 0;
            visibleChoices = null;
            isCurrentLineFullyRevealed = false;
            showingChoices = false;
            SetViewState(DialogViewMode.Hidden, null, null, null);
        }

        private void EnterNode(string nodeId) {
            currentNode = FindNode(nodeId);

            if (currentNode == null) {
                Debug.LogError($"DialogService: node '{nodeId}' not found in dialog '{currentDialog.dialogId}'.");
                EndDialog();
                return;
            }

            currentLineIndex = 0;
            showingChoices = false;
            visibleChoices = null;
            isCurrentLineFullyRevealed = false;

            ExecuteActions(currentNode.onEnterActions);

            if (currentNode.lines == null || currentNode.lines.Length == 0) {
                ShowChoicesOrEnd();
                return;
            }

            EmitCurrentLine();
        }

        private void EmitCurrentLine() {
            isCurrentLineFullyRevealed = false;
            var line = currentNode.lines[currentLineIndex];
            PlayLineSound(currentNode.speaker, line.soundId);
            SetViewState(DialogViewMode.Line, currentNode.speaker, line.text, null);
        }

        private void ShowChoicesOrEnd() {
            if (TryShowChoices()) {
                return;
            }

            EndDialog();
        }

        private bool TryShowChoices() {
            var state = G.Game.playerState;
            visibleChoices = FilterVisibleChoices(currentNode.choices, state);

            // Auto-continue: single choice with empty text
            if (visibleChoices.Count == 1 && string.IsNullOrEmpty(visibleChoices[0].text)) {
                PickChoice(0);
                return true;
            }

            if (visibleChoices.Count == 0) {
                visibleChoices = null;
                return false;
            }

            showingChoices = true;
            SetViewState(DialogViewMode.Choices, currentNode.speaker, GetCurrentLineText(), visibleChoices);
            return true;
        }

        private bool IsLastLineInCurrentNode() {
            if (currentNode?.lines == null || currentNode.lines.Length == 0) {
                return false;
            }

            return currentLineIndex >= currentNode.lines.Length - 1;
        }

        private string GetCurrentLineText() {
            if (currentNode?.lines == null) {
                return null;
            }

            if (currentLineIndex < 0 || currentLineIndex >= currentNode.lines.Length) {
                return null;
            }

            return currentNode.lines[currentLineIndex].text;
        }

        private void PlayLineSound(string speaker, string soundId) {
            StopSpeechSound();

            var library = G.Config.DialogSoundLibrary;
            if (library == null || G.Audio == null) {
                return;
            }

            var cue = library.Resolve(speaker, soundId);
            if (cue != null) {
                activeSpeechSound = G.Audio.Play2DTracked(cue);
            }
        }

        private void StopSpeechSound() {
            if (activeSpeechSound != null && activeSpeechSound.IsValid) {
                activeSpeechSound.Stop(0f);
            }

            activeSpeechSound = null;
        }

        private void SetViewState(DialogViewMode mode, string speaker, string text, IReadOnlyList<DialogChoice> choices) {
            currentViewState = new DialogViewState(mode, speaker, text, choices);
            StateChanged?.Invoke(currentViewState);
        }

        private DialogNode FindNode(string nodeId) {
            if (currentDialog.nodes == null) {
                return null;
            }

            for (int i = 0; i < currentDialog.nodes.Length; i++) {
                if (currentDialog.nodes[i].nodeId == nodeId) {
                    return currentDialog.nodes[i];
                }
            }

            return null;
        }

        private static List<DialogChoice> FilterVisibleChoices(DialogChoice[] choices, PlayerState state) {
            var result = new List<DialogChoice>();

            if (choices == null) {
                return result;
            }

            for (int i = 0; i < choices.Length; i++) {
                if (AreConditionsMet(choices[i].conditions, state)) {
                    result.Add(choices[i]);
                }
            }

            return result;
        }

        private static bool AreConditionsMet(DialogCondition[] conditions, PlayerState state) {
            if (conditions == null || conditions.Length == 0) {
                return true;
            }

            for (int i = 0; i < conditions.Length; i++) {
                if (!EvaluateCondition(conditions[i], state)) {
                    return false;
                }
            }

            return true;
        }

        private static bool EvaluateCondition(DialogCondition cond, PlayerState state) {
            switch (cond.type) {
                case ConditionType.HasItem:
                    var minCount = Mathf.Max(cond.intParam, 1);
                    return state.InventoryModel.GetCount(cond.stringParam) >= minCount;

                case ConditionType.DoesNotHaveItem:
                    // If intParam is set, treat this as "has fewer than N items" for dialog fallback branches.
                    var maxCountExclusive = Mathf.Max(cond.intParam, 1);
                    return state.InventoryModel.GetCount(cond.stringParam) < maxCountExclusive;

                case ConditionType.FlagSet:
                    return state.HasFlag(cond.stringParam);

                case ConditionType.FlagNotSet:
                    return !state.HasFlag(cond.stringParam);

                default:
                    Debug.LogWarning($"DialogService: unknown condition type '{cond.type}'.");
                    return false;
            }
        }

        private static void ExecuteActions(DialogAction[] actions) {
            if (actions == null) {
                return;
            }

            var state = G.Game.playerState;

            for (int i = 0; i < actions.Length; i++) {
                ExecuteAction(actions[i], state);
            }
        }

        private static void ExecuteAction(DialogAction action, PlayerState state) {
            switch (action.type) {
                case DialogActionType.GiveItem:
                    state.InventoryModel.Add(action.stringParam, action.intParam);
                    break;

                case DialogActionType.RemoveItem:
                    state.InventoryModel.Remove(action.stringParam, action.intParam);
                    break;

                case DialogActionType.SetFlag:
                    state.SetFlag(action.stringParam);
                    break;

                case DialogActionType.ClearFlag:
                    state.ClearFlag(action.stringParam);
                    break;

                default:
                    Debug.LogWarning($"DialogService: unknown action type '{action.type}'.");
                    break;
            }
        }
    }
}
