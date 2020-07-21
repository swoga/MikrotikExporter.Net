using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace MikrotikExporter.Discover
{
    class StaticConfig
    {
        public string[] Targets { get; set; }
        public Dictionary<string, string> Labels { get; set; }
    }
}
