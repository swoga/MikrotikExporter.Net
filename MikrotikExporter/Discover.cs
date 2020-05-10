using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace MikrotikExporter.Discover
{
    class StaticConfig
    {
        [JsonPropertyName("targets")]
        public string[] Targets { get; set; }
        [JsonPropertyName("labels")]
        public Dictionary<string, string> Lables { get; set; }
    }
}
