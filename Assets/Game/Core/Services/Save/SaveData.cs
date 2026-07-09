using System;
using System.Collections.Generic;
using Game.Core.Services.SceneState;
using Game.Features.Characters.Hero;

namespace Game.Core.Services.Save {
    /// <summary>
    /// Root of the on-disk save file: the whole playthrough serialized as one JSON object.
    /// All members are Unity-serializable so JsonUtility can round-trip them.
    /// </summary>
    [Serializable]
    public class SaveData {
        public int version;

        // Global player progression (inventory, coins, stats, flags, HP, IsArmed).
        public PlayerState player;

        // Active resting point. CheckpointRef is a struct, so a separate flag records absence.
        public bool hasCheckpoint;
        public CheckpointRef checkpoint;
        public List<string> discoveredCheckpoints = new();

        // Permanent world state (opened doors, consumed switches, destroyed props).
        public SceneStateSaveData sceneState;
    }
}
