using System;
using System.Collections.Generic;
using System.Text;
using YamlDotNet.Serialization;

namespace MikrotikExporter.Configuration
{
    public class SubConfig
    {
        [YamlMember(Alias = "modules")]
        public Dictionary<string, Module> Modules { get; protected set; } = new Dictionary<string, Module>();

        [YamlMember(Alias = "module_extensions")]
        public Dictionary<string, ModuleExtension> ModuleExtensions { get; protected set; } = new Dictionary<string, ModuleExtension>();

        [YamlMember(Alias = "targets")]
        public Dictionary<string, Target> Targets { get; protected set; } = new Dictionary<string, Target>();
    }
}
