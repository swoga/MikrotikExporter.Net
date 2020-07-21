using System.Collections.Generic;

namespace MikrotikExporter.Discover
{
    class StaticConfig
    {
        public string[] Targets { get; set; }
        public Dictionary<string, string> Labels { get; set; }
    }
}
