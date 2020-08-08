using System;
using System.Collections.Generic;
using System.Text;
using YamlDotNet.Serialization;

namespace MikrotikExporter.Configuration
{
    public class ModuleFile
    {
        [YamlMember(Alias = "modules")]
        public Dictionary<string, List<ModuleCommand>> Modules { get; protected set; } = new Dictionary<string, List<ModuleCommand>>();
    }
}
