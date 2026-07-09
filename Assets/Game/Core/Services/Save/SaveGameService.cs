using System;
using System.Collections.Generic;
using System.IO;
using Game.Core.Bootstrap;
using Game.Core.Models.Inventory;
using Game.Core.Services.SceneState;
using UnityEngine;

namespace Game.Core.Services.Save {
    /// <summary>
    /// Owns cross-session persistence of the whole playthrough: player progression
    /// (inventory, coins, stats, flags, HP), the active checkpoint, and permanent world
    /// state. Writes a single JSON file under <see cref="Application.persistentDataPath"/>
    /// and reloads it on launch so the main menu can offer Continue.
    ///
    /// The session tier of scene state is deliberately never persisted: on Continue the
    /// hero respawns at the last bonfire with transient world state reset, exactly matching
    /// in-session death/rest behavior — only permanent progress and resources carry over.
    ///
    /// Created dynamically by GInit, so per project service rules it must NOT use
    /// [SerializeField]; it reads everything it needs from the other G services.
    /// </summary>
    public class SaveGameService : MonoBehaviour {
        private const int SaveVersion = 1;
        private const string SaveFileName = "save.json";

        private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        private InventoryModel boundInventory;
        private bool dirty;

        /// <summary>
        /// Wires up save triggers. Must be called after the persisted services and the final
        /// PlayerState exist (i.e. after <see cref="LoadIntoServices"/>), and after
        /// SceneStateService has subscribed to BeforeUnload so its capture runs before ours.
        /// </summary>
        public void Init() {
            if (G.SceneTravel != null) {
                G.SceneTravel.BeforeUnload += OnBeforeUnload;
            }

            if (G.Checkpoint != null) {
                G.Checkpoint.OnCheckpointChanged += OnCheckpointChanged;
            }

            if (G.Game != null) {
                G.Game.PlayerStateChanged += RebindInventory;
            }

            RebindInventory();
        }

        private void OnDestroy() {
            if (G.SceneTravel != null) {
                G.SceneTravel.BeforeUnload -= OnBeforeUnload;
            }

            if (G.Checkpoint != null) {
                G.Checkpoint.OnCheckpointChanged -= OnCheckpointChanged;
            }

            if (G.Game != null) {
                G.Game.PlayerStateChanged -= RebindInventory;
            }

            if (boundInventory != null) {
                boundInventory.OnChange -= OnInventoryChanged;
            }
        }

        // ---- Triggers ----

        // Fires after SceneStateService has captured the unloading scene, so persistent world
        // changes committed on room exit (opened doors) are already in the store.
        private void OnBeforeUnload(UnityEngine.SceneManagement.Scene scene) {
            RequestSave();
        }

        // Fires when the player rests at a bonfire (Activate) — the natural checkpoint save.
        private void OnCheckpointChanged(CheckpointRef? checkpoint) {
            RequestSave();
        }

        // Fires on any resource/item/coin change, so nothing collected is ever lost.
        private void OnInventoryChanged(InventoryChangeEvent change) {
            RequestSave();
        }

        // Re-binds the inventory hook to the current PlayerState instance. New Game and loading
        // a save replace the PlayerState (and its InventoryModel), so the old hook must move.
        private void RebindInventory() {
            var inventory = G.Game != null && G.Game.playerState != null
                ? G.Game.playerState.InventoryModel
                : null;

            if (inventory == boundInventory) {
                return;
            }

            if (boundInventory != null) {
                boundInventory.OnChange -= OnInventoryChanged;
            }

            boundInventory = inventory;

            if (boundInventory != null) {
                boundInventory.OnChange += OnInventoryChanged;
            }
        }

        /// <summary>Marks the save dirty. Writes are coalesced to at most one per frame.</summary>
        public void RequestSave() {
            dirty = true;
        }

        private void LateUpdate() {
            if (dirty) {
                Flush();
            }
        }

        // On mobile a backgrounded app may be killed without OnApplicationQuit, so flush here too.
        private void OnApplicationPause(bool paused) {
            if (paused && dirty) {
                Flush();
            }
        }

        private void OnApplicationQuit() {
            if (dirty) {
                Flush();
            }
        }

        private void Flush() {
            dirty = false;
            SaveNow();
        }

        // ---- Save ----

        /// <summary>
        /// Writes the current playthrough to disk. No-op until a checkpoint exists: without a
        /// resting point there is nothing to Continue to, and skipping the write keeps New Game
        /// (which clears the checkpoint) from re-creating a file it just deleted.
        /// </summary>
        public void SaveNow() {
            if (G.Checkpoint == null || !G.Checkpoint.Current.HasValue) {
                return;
            }

            var data = new SaveData {
                version = SaveVersion,
                player = G.Game.playerState,
                hasCheckpoint = true,
                checkpoint = G.Checkpoint.Current.Value,
                discoveredCheckpoints = new List<string>(G.Checkpoint.DiscoveredKeys),
                sceneState = G.SceneState.ExportPersistent(),
            };

            try {
                var json = JsonUtility.ToJson(data);
                WriteAtomic(SavePath, json);
            } catch (Exception e) {
                Debug.LogWarning($"[SaveGameService] Failed to write save file: {e.Message}");
            }
        }

        // Writes to a temp file then swaps it in, so a crash mid-write can't corrupt the save.
        private static void WriteAtomic(string path, string json) {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json);

            if (File.Exists(path)) {
                File.Replace(tmp, path, null);
            } else {
                File.Move(tmp, path);
            }
        }

        // ---- Load ----

        /// <summary>
        /// Applies a saved playthrough (if any) over the freshly-created services. Call once at
        /// startup, after G.Game.Init() and before HUD/debug seeding. A missing, unreadable, or
        /// version-mismatched file is ignored so the game simply starts fresh.
        /// </summary>
        public void LoadIntoServices() {
            var path = SavePath;
            if (!File.Exists(path)) {
                return;
            }

            SaveData data;
            try {
                var json = File.ReadAllText(path);
                data = JsonUtility.FromJson<SaveData>(json);
            } catch (Exception e) {
                Debug.LogWarning($"[SaveGameService] Failed to read save file, starting fresh: {e.Message}");
                return;
            }

            if (data == null || data.version != SaveVersion || data.player == null) {
                Debug.LogWarning("[SaveGameService] Save file missing or incompatible; starting fresh.");
                return;
            }

            // JsonUtility bypasses the PlayerState constructor, so its transient panel models
            // are null until rebuilt.
            data.player.RebuildTransient();
            G.Game.SetPlayerState(data.player);

            CheckpointRef? checkpoint = data.hasCheckpoint ? data.checkpoint : (CheckpointRef?)null;
            G.Checkpoint.RestoreState(checkpoint, data.discoveredCheckpoints);

            G.SceneState.ImportPersistent(data.sceneState);
        }

        /// <summary>Deletes the save file. Called when starting a New Game.</summary>
        public void DeleteSave() {
            dirty = false;

            try {
                if (File.Exists(SavePath)) {
                    File.Delete(SavePath);
                }
            } catch (Exception e) {
                Debug.LogWarning($"[SaveGameService] Failed to delete save file: {e.Message}");
            }
        }
    }
}
