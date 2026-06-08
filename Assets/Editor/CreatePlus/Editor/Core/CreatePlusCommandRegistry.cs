using System.Collections.Generic;
using UnityEngine;

namespace CreatePlus.Core {
    /// <summary>
    /// Aggregates create commands from all registered providers into a single list.
    ///
    /// For the MVP the registry is populated from a small set of static providers. The provider model
    /// is designed so automatic discovery (MenuItem scanning, CreateAssetMenu attributes, project
    /// factories) can be added later without changing consumers.
    ///
    /// This class is UI-independent: it knows nothing about IMGUI or UI Toolkit.
    /// </summary>
    public static class CreatePlusCommandRegistry {
        static readonly List<ICreatePlusCommandProvider> providers = new List<ICreatePlusCommandProvider>();
        static readonly List<CreatePlusCommand> commands = new List<CreatePlusCommand>();
        static bool built;
        static bool defaultsAdded;

        /// <summary>All resolved commands. Builds on first access.</summary>
        public static IReadOnlyList<CreatePlusCommand> Commands {
            get {
                EnsureBuilt();
                return commands;
            }
        }

        /// <summary>Registers a provider. Triggers a rebuild on next access.</summary>
        public static void RegisterProvider(ICreatePlusCommandProvider provider) {
            if (provider == null) {
                return;
            }

            providers.Add(provider);
            built = false;
        }

        /// <summary>Clears all registered providers and cached commands.</summary>
        public static void Clear() {
            providers.Clear();
            commands.Clear();
            built = false;
            defaultsAdded = false;
        }

        /// <summary>Forces the command list to be rebuilt from the providers.</summary>
        public static void Rebuild() {
            built = false;
            EnsureBuilt();
        }

        /// <summary>Finds a command by its stable id, or null if not registered.</summary>
        public static CreatePlusCommand Find(string id) {
            if (string.IsNullOrEmpty(id)) {
                return null;
            }

            EnsureBuilt();
            for (int i = 0; i < commands.Count; i++) {
                if (commands[i].Id == id) {
                    return commands[i];
                }
            }

            return null;
        }

        static void EnsureBuilt() {
            if (built) {
                return;
            }

            EnsureDefaultProviders();

            commands.Clear();
            var seenIds = new HashSet<string>();
            foreach (ICreatePlusCommandProvider provider in providers) {
                IEnumerable<CreatePlusCommand> contributed = provider.GetCommands();
                if (contributed == null) {
                    continue;
                }

                foreach (CreatePlusCommand command in contributed) {
                    if (command == null || string.IsNullOrEmpty(command.Id)) {
                        Debug.LogWarning("[Create Plus] Skipping a command with no id from provider " + provider.GetType().Name);
                        continue;
                    }

                    if (!seenIds.Add(command.Id)) {
                        Debug.LogWarning("[Create Plus] Duplicate command id ignored: " + command.Id);
                        continue;
                    }

                    commands.Add(command);
                }
            }

            built = true;
        }

        static void EnsureDefaultProviders() {
            // The built-in providers are registered exactly once. Project code may register additional
            // providers before or after this, and they are all aggregated together.
            if (defaultsAdded) {
                return;
            }

            defaultsAdded = true;
            providers.Insert(0, new Commands.CreatePlusBuiltInCommands());
            providers.Insert(1, new Commands.CreatePlusProjectCommands());
        }
    }
}
