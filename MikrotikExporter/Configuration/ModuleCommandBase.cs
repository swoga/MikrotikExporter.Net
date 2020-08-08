using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace MikrotikExporter.Configuration
{
    public abstract class ModuleCommandBase<TLabel, TMetric, TSubCommand>
    {
        [Required]
        [YamlMember(Alias = "command")]
        public string Command { get; protected set; }

        [YamlMember(Alias = "command_timeout")]
        public TimeSpan? CommandTimeout { get; protected set; }

        [YamlMember(Alias = "prefix")]
        public string Prefix { get; protected set; }

        [YamlMember(Alias = "labels")]
        public List<TLabel> Labels { get; protected set; } = new List<TLabel>();

        [YamlMember(Alias = "metrics")]
        public List<TMetric> Metrics { get; protected set; } = new List<TMetric>();

        [YamlMember(Alias = "variables")]
        public List<TLabel> Variables { get; protected set; } = new List<TLabel>();

        [YamlMember(Alias = "sub_commands")]
        public List<TSubCommand> SubCommands { get; protected set; } = new List<TSubCommand>();
    }
}
