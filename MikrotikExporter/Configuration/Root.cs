using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace MikrotikExporter.Configuration
{
    public class Root : ModuleFile
    {
        [YamlMember(Alias = "global")]
        public Global Global { get; private set; } = new Global();

        [YamlMember(Alias = "targets")]
        public Dictionary<string, Target> Targets { get; private set; } = new Dictionary<string, Target>();
    }
}
