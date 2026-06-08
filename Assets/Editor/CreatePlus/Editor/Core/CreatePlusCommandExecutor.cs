using System;
using UnityEngine;

namespace CreatePlus.Core {
    /// <summary>
    /// Runs a command and records its usage. UI-independent: the window calls this and, on success,
    /// closes itself. Execution side effects (creating and pinging assets) live inside each command's
    /// <see cref="CreatePlusCommand.Execute"/> action, not here.
    /// </summary>
    public static class CreatePlusCommandExecutor {
        /// <summary>
        /// Executes the command against the context. Returns true only when the command actually did
        /// something (it is implemented and did not throw); in that case usage and recent history are
        /// updated. Placeholder commands log a message and return false so the palette stays open.
        /// </summary>
        public static bool Execute(CreatePlusCommand command, CreatePlusContext context) {
            if (command == null || command.Execute == null) {
                return false;
            }

            if (!command.IsEnabled) {
                Debug.LogWarning("[Create Plus] Command is disabled: " + command.DisplayName +
                                 (string.IsNullOrEmpty(command.DisabledReason) ? string.Empty : " (" + command.DisabledReason + ")"));
                return false;
            }

            try {
                command.Execute(context ?? CreatePlusContext.Empty);
            } catch (Exception e) {
                Debug.LogError("[Create Plus] Command '" + command.DisplayName + "' failed: " + e);
                return false;
            }

            if (!command.IsImplemented) {
                return false;
            }

            CreatePlusSettingsStore.RecordUsage(command.Id);
            CreatePlusSettingsStore.AddRecent(command.Id);
            return true;
        }
    }
}
