using System;
using System.Collections.Generic;
using Core;
using Game.Core.Bootstrap;
using Game.Core.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Core.Services.Scene {
    /// <summary>
    /// Centralized menu navigation manager.
    /// Instantiates menus from configured prefabs and keeps a stack-like history of opened
    /// <see cref="MenuWindow"/> instances,
    /// handles open/close transitions, remembers selected UI elements per window,
    /// and switches input/time between gameplay and UI modes.
    /// </summary>
    public sealed class MenuManager : MonoBehaviour {
        private const string MainMenuSceneName = "MainMenuScene";

        private sealed class WindowEntry {
            public MenuWindow Window;
            public GameObject LastSelected;
        }

        private readonly List<WindowEntry> windowEntries = new();

        private InputAction pauseAction;
        private InputAction cancelAction;
        private Canvas menuCanvas;
        private bool isTransitionInProgress;
        private int openedOnFrame;
        private int TopIndex => windowEntries.Count - 1;

        /// <summary>
        /// Fires when the first window opens and UI mode is entered.
        /// </summary>
        public event Action OnMenuOpened;

        /// <summary>
        /// Fires when all windows are closed and gameplay mode is restored.
        /// </summary>
        public event Action OnMenuClosed;

        /// <summary>
        /// Fires when the background-dim requirement changes (true = should be dimmed).
        /// Driven by the <see cref="MenuWindow.DimsBackground"/> flags of the open windows.
        /// </summary>
        public event Action<bool> OnDimStateChanged;

        private bool currentDimState;

        /// <summary>
        /// Whether the background should currently be dimmed (any open window requests it).
        /// </summary>
        public bool ShouldDimBackground => ComputeShouldDim();

        /// <summary>
        /// The canvas used to host menu windows. Lazily created.
        /// </summary>
        public Canvas Canvas => ResolveMenuCanvas();

        /// <summary>
        /// Gets whether at least one valid window is currently managed/open.
        /// </summary>
        public bool IsAnyWindowOpen {
            get {
                PruneInvalidWindows();
                return isTransitionInProgress || windowEntries.Count > 0;
            }
        }

        private void Awake() {
            if (G.Input == null) {
                Debug.LogError("MenuManager requires InputService to be initialized before Awake.");
                enabled = false;
                return;
            }

            pauseAction = G.Input.Player.Pause;
            cancelAction = G.Input.UI.Cancel;
        }

        private void OnEnable() {
            if (pauseAction != null) {
                pauseAction.performed += OnPausePerformed;
            }

            if (cancelAction != null) {
                cancelAction.performed += OnCancelPerformed;
            }
        }

        private void OnDisable() {
            if (pauseAction != null) {
                pauseAction.performed -= OnPausePerformed;
            }

            if (cancelAction != null) {
                cancelAction.performed -= OnCancelPerformed;
            }
        }

        private void OnDestroy() {
            // Safety net for scene switches while close transitions are in flight.
            Time.timeScale = 1f;
        }

        private void OnPausePerformed(InputAction.CallbackContext context) {
            if (IsAnyWindowOpen) {
                Debug.Log("Any window open");
                return;
            }

            if (IsMainMenuSceneActive()) {
                Debug.Log("Main menu scene active");
                return;
            }

            // Remember the frame so OnCancelPerformed ignores the same Escape press
            // that opened the menu (Pause and Cancel share the Escape key).
            openedOnFrame = Time.frameCount;
            OpenPauseMenu();
        }

        private void OnCancelPerformed(InputAction.CallbackContext context) {
            if (isTransitionInProgress || Time.frameCount == openedOnFrame) {
                return;
            }

            if (!TryGetTopEntry(out var topEntry)) {
                return;
            }

            if (IsMainMenuWindow(topEntry.Window)) {
                QuitGame();
                return;
            }

            if (!topEntry.Window.CloseOnCancel) {
                return;
            }

            CloseTopWindow();
        }

        /// <summary>
        /// Opens the main menu configured in <see cref="G.Config"/>.
        /// </summary>
        public void OpenMainMenu() {
            OpenMenu(G.Config.MainMenu);
        }

        /// <summary>
        /// Opens the options menu configured in <see cref="G.Config"/>.
        /// </summary>
        public void OpenOptionsMenu() {
            OpenMenu(G.Config.OptionsMenu);
        }

        /// <summary>
        /// Opens the pause menu configured in <see cref="G.Config"/>.
        /// </summary>
        public void OpenPauseMenu() {
            OpenMenu(G.Config.PauseMenu);
        }
        
        public void OpenInventory() {
            OpenMenu(G.Config.InventoryPanel);
        }

        /// <summary>
        /// Opens a menu from a configured prefab reference.
        /// If a window of the same runtime type already exists in history, the stack is rewound to it.
        /// Otherwise a new instance is created and pushed to the top.
        /// </summary>
        /// <param name="menuPrefab">Menu prefab/component reference to open.</param>
        public T OpenMenu<T>(T menuPrefab) where T : MenuWindow {
            if (isTransitionInProgress) {
                return null;
            }

            if (menuPrefab == null) {
                Debug.LogWarning("Requested menu is null. Assign menu prefabs in MainConfig.");
                return null;
            }

            if (!EnsureEventSystem()) {
                return null;
            }

            if (TryRestoreManagedWindow(menuPrefab.GetType())) {
                return null;
            }

            var window = InstantiateWindow(menuPrefab);
            OpenWindow(window);
            return window;
        }

        /// <summary>
        /// Closes the currently top-most window and returns to the previous one if present.
        /// When the last window is closed, UI mode is exited.
        /// </summary>
        public void CloseTopWindow() {
            if (isTransitionInProgress) {
                return;
            }

            CloseTopWindowInternal(restorePreviousWindow: true);
        }

        /// <summary>
        /// Closes all managed windows and exits UI mode.
        /// All managed window instances are destroyed.
        /// </summary>
        public void CloseAll() {
            CloseAll(null);
        }

        /// <summary>
        /// Closes all managed windows, exits UI mode and invokes callback after close transitions complete.
        /// </summary>
        /// <param name="afterClosed">Callback invoked after all windows are closed and mode is restored.</param>
        public void CloseAll(Action afterClosed) {
            if (isTransitionInProgress) {
                return;
            }

            PruneInvalidWindows();

            CloseAllInternal(afterClosed);
        }

        private void OpenWindow(MenuWindow window) {
            PruneInvalidWindows();

            if (window == null) {
                return;
            }

            var newEntry = new WindowEntry {
                Window = window,
                LastSelected = null
            };

            if (windowEntries.Count == 0) {
                EnterMenuMode();
                windowEntries.Add(newEntry);
                window.Open(newEntry.LastSelected);

                ApplyPauseState();
                return;
            }

            var topEntry = windowEntries[TopIndex];
            topEntry.LastSelected = GetCurrentSelection(topEntry.Window);

            isTransitionInProgress = true;
            topEntry.Window.Close(() => {
                windowEntries.Add(newEntry);
                window.Open(newEntry.LastSelected);

                ApplyPauseState();
                isTransitionInProgress = false;
            });
        }

        private bool TryRestoreManagedWindow(Type windowType) {
            PruneInvalidWindows();

            for (var i = TopIndex; i >= 0; i--) {
                var entry = windowEntries[i];
                if (entry.Window == null || !windowType.IsInstanceOfType(entry.Window)) {
                    continue;
                }

                if (i == TopIndex) {
                    if (!entry.Window.gameObject.activeSelf) {
                        entry.Window.Open(entry.LastSelected);
                    }

                    return true;
                }

                RewindToIndex(i);

                return true;
            }

            return false;
        }

        private T InstantiateWindow<T>(T menuPrefab) where T : MenuWindow {
            var canvas = ResolveMenuCanvas();
            if (canvas == null) {
                return null;
            }

            var window = Instantiate(menuPrefab, canvas.transform, false);
            window.gameObject.SetActive(false);
            return window;
        }

        private Canvas ResolveMenuCanvas() {
            if (menuCanvas != null) {
                return menuCanvas;
            }

            var uiCanvas = GameObject.Find(CoreConst.UICanvasName);
            if (uiCanvas == null) {
                var globalRoot = SceneUtils.GetOrCreateRootObject(CoreConst.GlobalRootName);
                var canvasesRoot = SceneUtils.GetOrCreateObject(CoreConst.CanvasesName, globalRoot.transform);
                uiCanvas = SceneUtils.GetOrCreateObject(CoreConst.UICanvasName, canvasesRoot.transform);
            }

            menuCanvas = uiCanvas.GetComponent<Canvas>();
            if (menuCanvas == null) {
                menuCanvas = uiCanvas.AddComponent<Canvas>();
                menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            menuCanvas.sortingOrder = 100;

            if (uiCanvas.GetComponent<CanvasScaler>() == null) {
                var scaler = uiCanvas.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = CoreConst.ReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (uiCanvas.GetComponent<GraphicRaycaster>() == null) {
                uiCanvas.AddComponent<GraphicRaycaster>();
            }

            return menuCanvas;
        }

        private bool EnsureEventSystem() {
            var existingEventSystem = EventSystem.current ?? FindAnyObjectByType<EventSystem>();
            if (existingEventSystem != null) {
                return EnsureUiInputModule(existingEventSystem.gameObject);
            }

            Debug.LogError(
                "No EventSystem found. Add an EventSystem with InputSystemUIInputModule to the scene (or make sure GlobalRoot prefab is present in scene)."
            );
            return false;
        }

        private bool EnsureUiInputModule(GameObject eventSystemGo) {
            if (eventSystemGo.GetComponent<InputSystemUIInputModule>() != null) {
                return true;
            }

            if (eventSystemGo.GetComponent<StandaloneInputModule>() != null) {
                return true;
            }

            Debug.LogError(
                $"EventSystem '{eventSystemGo.name}' has no UI input module. " +
                "Add InputSystemUIInputModule to handle UI clicks."
            );
            return false;
        }

        private void PruneInvalidWindows() {
            if (windowEntries.Count == 0) {
                return;
            }

            var removedAny = false;

            for (var i = TopIndex; i >= 0; i--) {
                if (windowEntries[i].Window == null) {
                    windowEntries.RemoveAt(i);
                    removedAny = true;
                }
            }

            if (removedAny && windowEntries.Count == 0 && !isTransitionInProgress) {
                ExitMenuMode();

                return;
            }

            if (removedAny) {
                ApplyPauseState();

            }
        }

        private bool TryGetTopEntry(out WindowEntry entry) {
            PruneInvalidWindows();

            if (windowEntries.Count == 0) {
                entry = null;
                return false;
            }

            entry = windowEntries[TopIndex];
            return true;
        }

        private GameObject GetCurrentSelection(MenuWindow window) {
            if (window == null || EventSystem.current == null) {
                return null;
            }

            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null) {
                return null;
            }

            if (!selected.transform.IsChildOf(window.transform) && selected != window.gameObject) {
                return null;
            }

            return selected;
        }

        private void CloseAndDestroy(WindowEntry entry, Action afterClosed = null) {
            if (entry.Window == null) {
                afterClosed?.Invoke();
                return;
            }

            entry.Window.Close(() => {
                if (entry.Window != null) {
                    Destroy(entry.Window.gameObject);
                }

                afterClosed?.Invoke();
            });
        }

        private void CloseTopWindowInternal(bool restorePreviousWindow, Action afterClosed = null) {
            if (!TryGetTopEntry(out var closingEntry)) {
                afterClosed?.Invoke();
                return;
            }

            isTransitionInProgress = true;

            CloseAndDestroy(closingEntry, () => {
                RemoveEntry(closingEntry);


                if (restorePreviousWindow) {
                    if (TryGetTopEntry(out var nextEntry)) {
                        nextEntry.Window.Open(nextEntry.LastSelected);
                    } else {
                        ExitMenuMode();
                    }
                }

                ApplyPauseState();
                isTransitionInProgress = false;
                afterClosed?.Invoke();
            });
        }

        private void CloseAllInternal(Action afterClosed = null) {
            if (windowEntries.Count == 0) {
                ExitMenuMode();
                afterClosed?.Invoke();
                return;
            }

            CloseTopWindowInternal(
                restorePreviousWindow: false,
                afterClosed: () => CloseAllInternal(afterClosed)
            );
        }

        private void RewindToIndex(int targetIndex) {
            if (TopIndex <= targetIndex) {
                return;
            }

            CloseTopWindowInternal(
                restorePreviousWindow: true,
                afterClosed: () => RewindToIndex(targetIndex)
            );
        }

        private void EnterMenuMode() {
            OnMenuOpened?.Invoke();

            if (G.Input == null) {
                return;
            }

            G.Input.Player.Disable();
            G.Input.UI.Enable();
        }

        private void ExitMenuMode() {
            OnMenuClosed?.Invoke();
            NotifyDimState();

            Time.timeScale = 1f;

            if (G.Input == null) {
                return;
            }

            G.Input.UI.Disable();
            G.Input.Player.Enable();
        }

        private void ApplyPauseState() {
            var shouldPause = false;

            for (var i = 0; i < windowEntries.Count; i++) {
                var window = windowEntries[i].Window;
                if (window == null || !window.gameObject.activeSelf) {
                    continue;
                }

                if (!window.PausesGame) {
                    continue;
                }

                shouldPause = true;
                break;
            }

            Time.timeScale = shouldPause ? 0f : 1f;
            NotifyDimState();
        }

        private bool ComputeShouldDim() {
            for (var i = 0; i < windowEntries.Count; i++) {
                var window = windowEntries[i].Window;
                if (window == null || !window.gameObject.activeSelf) {
                    continue;
                }

                if (window.DimsBackground) {
                    return true;
                }
            }

            return false;
        }

        private void NotifyDimState() {
            var shouldDim = ComputeShouldDim();
            if (shouldDim == currentDimState) {
                return;
            }

            currentDimState = shouldDim;
            OnDimStateChanged?.Invoke(shouldDim);
        }

        private void RemoveEntry(WindowEntry entry) {
            if (entry == null) {
                return;
            }

            for (var i = TopIndex; i >= 0; i--) {
                if (!ReferenceEquals(windowEntries[i], entry)) {
                    continue;
                }

                windowEntries.RemoveAt(i);
                return;
            }
        }

        private bool IsMainMenuSceneActive() {
            return SceneManager.GetActiveScene().name == MainMenuSceneName;
        }

        private bool IsMainMenuWindow(MenuWindow window) {
            if (window == null || G.Config?.MainMenu == null) {
                return false;
            }

            return G.Config.MainMenu.GetType().IsInstanceOfType(window);
        }

        private void QuitGame() {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
