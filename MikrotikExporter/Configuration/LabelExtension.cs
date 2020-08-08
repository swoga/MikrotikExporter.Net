using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace MikrotikExporter.Configuration
{
    public class LabelExtension : Label
    {
        [Required]
        [YamlMember(Alias = "extension_action")]
        public ExtensionEnum ExtensionAction { get; protected set; }
    }
}
