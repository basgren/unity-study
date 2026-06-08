using System.Collections.Generic;

namespace CreatePlus.Core {
    /// <summary>
    /// Extension point for supplying create commands to the registry. Project code or packages can
    /// implement this and register it via <see cref="CreatePlusCommandRegistry.RegisterProvider"/> to
    /// add custom commands without touching the plugin.
    /// </summary>
    public interface ICreatePlusCommandProvider {
        /// <summary>Returns the commands contributed by this provider.</summary>
        IEnumerable<CreatePlusCommand> GetCommands();
    }
}
