using System;
using System.Collections;
using System.Collections.Generic;
using Core.Audio;
using Game.Core.Bootstrap;
using Game.Core.Models.Dialog;
using Game.Features.Characters.Hero;
using Game.UI.ShopInventory;
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
        public readonly IReadOnlyList<string> Choices;

        public DialogViewState(DialogViewMode mode, string speaker, string text, IReadOnlyList<string> choices) {
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

        /// <summary>
        /// Fires when a <see cref="DialogActionType.RaiseEvent"/> action runs; the argument is
        /// the action's stringParam (the event id). Scene/world systems (e.g. cutscenes) can
        /// subscribe to react to specific dialog nodes without the dialog system depending on them.
        /// </summary>
        public event Action<string> EventRaised;

        private DialogDef currentDialog;
        private DialogNode currentNode;
        private int currentLineIndex;
        private List<DialogChoice> visibleChoices;
        private DialogViewState currentViewState;
        private List<string> resolvedChoiceTexts;
        private bool isCurrentLineFullyRevealed;
        private bool showingChoices;
        private IAudioLoopHandle activeSpeechSound;
        private string pendingShopId;
        private string pendingShopReturnDialogId;
        private string pendingShopReturnNodeId;
        private string pendingShopReturnShopId;
        private int shopPurchaseBaseline;

        // Reserved flag the service raises after a shop visit so dialog nodes can
        // branch on whether the player bought anything. Cleared by the farewell nodes.
        private const string ShopBoughtFlag = "shop_bought";

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
            EnterNode(DialogEntryResolver.Resolve(currentDialog, G.Game.playerState));
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

            // If the choice opened a shop and also has a follow-up node, defer that
            // node until the shop window closes (see HandleShopClosedResume).
            if (pendingShopId != null && !string.IsNullOrEmpty(choice.nextNodeId)) {
                pendingShopReturnDialogId = currentDialog.dialogId;
                pendingShopReturnNodeId = choice.nextNodeId;
                pendingShopReturnShopId = pendingShopId;
                shopPurchaseBaseline = G.Game.playerState.GetTotalShopPurchases(pendingShopId);
                EndDialog();
                return;
            }

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
            resolvedChoiceTexts = null;
            isCurrentLineFullyRevealed = false;
            showingChoices = false;
            SetViewState(DialogViewMode.Hidden, null, null, null);

            if (pendingShopId != null) {
                var shopId = pendingShopId;
                pendingShopId = null;
                StartCoroutine(OpenShopDeferred(shopId));
            }
        }

        private IEnumerator OpenShopDeferred(string shopId) {
            // Wait until the dialog panel close transition finishes and the menu
            // stack is clear. MenuManager.OpenMenu returns null while a transition
            // is in progress, so we must wait it out.
            while (G.Menu.IsAnyWindowOpen) {
                yield return null;
            }

            var shop = G.Menu.OpenMenu(G.Config.ShopPanel);
            if (shop is ShopInventory shopInventory) {
                shopInventory.LoadShop(shopId);
            }

            // If this shop was opened from a choice that wants to resume the dialog,
            // wait for the shop window to close, then re-open the dialog at the
            // follow-up node.
            if (pendingShopReturnNodeId != null) {
                G.Menu.OnMenuClosed += HandleShopClosedResume;
            }
        }

        private void HandleShopClosedResume() {
            G.Menu.OnMenuClosed -= HandleShopClosedResume;

            var dialogId = pendingShopReturnDialogId;
            var nodeId = pendingShopReturnNodeId;
            var shopId = pendingShopReturnShopId;
            var baseline = shopPurchaseBaseline;

            pendingShopReturnDialogId = null;
            pendingShopReturnNodeId = null;
            pendingShopReturnShopId = null;
            shopPurchaseBaseline = 0;

            // Branch the farewell on whether the player bought anything this visit.
            var state = G.Game.playerState;
            if (state.GetTotalShopPurchases(shopId) > baseline) {
                state.SetFlag(ShopBoughtFlag);
            } else {
                state.ClearFlag(ShopBoughtFlag);
            }

            StartCoroutine(ResumeDialogDeferred(dialogId, nodeId));
        }

        private IEnumerator ResumeDialogDeferred(string dialogId, string nodeId) {
            // Wait until the shop close transition finishes and the menu stack is
            // clear before opening the dialog panel again.
            while (G.Menu.IsAnyWindowOpen) {
                yield return null;
            }

            currentDialog = DialogLoader.Load(dialogId);
            if (currentDialog == null) {
                yield break;
            }

            G.Menu.OpenMenu(G.Config.DialogPanel);
            EnterNode(nodeId);
        }

        private void EnterNode(string nodeId) {
            currentNode = FindNode(nodeId);

            if (currentNode == null) {
                Debug.LogError($"DialogService: node '{nodeId}' not found in dialog '{currentDialog.dialogId}'.");
                EndDialog();
                return;
            }

            if (currentNode.once) {
                G.Game.playerState.MarkNodeSeen(
                    DialogEntryResolver.SeenKey(currentDialog.dialogId, currentNode.nodeId));
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
            var speakerName = ResolveSpeakerName(currentNode.speaker);
            var lineText = G.Strings.Resolve(line.textKey);
            SetViewState(DialogViewMode.Line, speakerName, lineText, null);
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

            // Auto-continue: single choice with empty key
            if (visibleChoices.Count == 1 && string.IsNullOrEmpty(visibleChoices[0].textKey)) {
                PickChoice(0);
                return true;
            }

            if (visibleChoices.Count == 0) {
                visibleChoices = null;
                return false;
            }

            showingChoices = true;
            resolvedChoiceTexts = ResolveChoiceTexts(visibleChoices);
            var speakerName = ResolveSpeakerName(currentNode.speaker);
            SetViewState(DialogViewMode.Choices, speakerName, GetCurrentLineText(), resolvedChoiceTexts);
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

            return G.Strings.Resolve(currentNode.lines[currentLineIndex].textKey);
        }

        private string ResolveSpeakerName(string speakerId) {
            if (string.IsNullOrEmpty(speakerId)) {
                return "";
            }

            var key = $"dialog.{currentDialog.dialogId}.speaker.{speakerId}";
            return G.Strings.Resolve(key);
        }

        private static List<string> ResolveChoiceTexts(List<DialogChoice> choices) {
            var result = new List<string>(choices.Count);
            for (int i = 0; i < choices.Count; i++) {
                result.Add(G.Strings.Resolve(choices[i].textKey));
            }

            return result;
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

        private void SetViewState(DialogViewMode mode, string speaker, string text, IReadOnlyList<string> choices) {
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
                if (DialogConditionEvaluator.AreConditionsMet(choices[i].conditions, state)) {
                    result.Add(choices[i]);
                }
            }

            return result;
        }

        private void ExecuteActions(DialogAction[] actions) {
            if (actions == null) {
                return;
            }

            var state = G.Game.playerState;

            for (int i = 0; i < actions.Length; i++) {
                ExecuteAction(actions[i], state);
            }
        }

        private void ExecuteAction(DialogAction action, PlayerState state) {
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

                case DialogActionType.OpenShop:
                    pendingShopId = action.stringParam;
                    break;

                case DialogActionType.RaiseEvent:
                    EventRaised?.Invoke(action.stringParam);
                    break;

                default:
                    Debug.LogWarning($"DialogService: unknown action type '{action.type}'.");
                    break;
            }
        }
    }
}
