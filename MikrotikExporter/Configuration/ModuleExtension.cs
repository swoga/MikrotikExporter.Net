using System;
using System.Collections.Generic;
using System.Linq;

namespace MikrotikExporter.Configuration
{
    public class ModuleExtension : List<ModuleCommandExtension>
    {
        internal bool TryExtendModule(Log log, Module module)
        {
            var success = true;
            foreach (var moduleCommand in module)
            {
                // find all extensions which commands match (normally only one)
                var moduleCommandExtensions = this.Where((x) => x.Command == moduleCommand.Command);
                foreach (var moduleCommandExtension in moduleCommandExtensions)
                {
                    success = moduleCommandExtension.TryExtendModuleCommand(log, moduleCommand) && success;
                }
            }
            return success;
        }
    }
}
