using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace MikrotikExporter.Configuration
{
    public class Target
    {
        [Required]
        [YamlMember(Alias = "host")]
        public string Host { get; set; }

        [YamlMember(Alias = "username")]
        public string Username { get; set; }

        [YamlMember(Alias = "password")]
        public string Password { get; set; }

        [YamlMember(Alias = "discover_labels")]
        public Dictionary<string, string> DiscoverLabels { get; private set; } = new Dictionary<string, string>();

        [YamlMember(Alias = "variables")]
        public Dictionary<string, string> Variables { get; private set; } = new Dictionary<string, string>();

        /// <summary>
        /// Default modules for this target, if ?module=xxx is omitted
        /// </summary>
        [YamlMember(Alias = "modules")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "<Pending>")]
        public string[] Modules { get; set; } = System.Array.Empty<string>();
    }
}
