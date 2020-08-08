using System;
using System.Collections.Generic;
using System.Text;
using YamlDotNet.Serialization;

namespace MikrotikExporter.Configuration
{
    public class ModuleFile
    {
        [YamlMember(Alias = "modules")]
        public Dictionary<string, Module> Modules { get; protected set; } = new Dictionary<string, Module>();

        [YamlMember(Alias = "module_extensions")]
        public Dictionary<string, List<ModuleCommandExtension>> ModuleExtensions { get; protected set; } = new Dictionary<string, List<ModuleCommandExtension>>();
    }
}
