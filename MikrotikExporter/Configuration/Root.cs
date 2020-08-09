using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace MikrotikExporter.Configuration
{
    public class Root : SubConfig
    {
        [YamlMember(Alias = "global")]
        public Global Global { get; private set; } = new Global();
    }
}
