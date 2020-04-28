using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace MikrotikExporter.Configuration
{
    public class Root
    {
        [YamlMember(Alias = "global")]
        public Global Global { get; private set; } = new Global();

        [YamlMember(Alias = "targets")]
        public Dictionary<string, Target> Targets { get; private set; } = new Dictionary<string, Target>();

        [YamlMember(Alias = "modules")]
        public Dictionary<string, List<ModuleCommand>> Modules { get; private set; } = new Dictionary<string, List<ModuleCommand>>();
    }
}
