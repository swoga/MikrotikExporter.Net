using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace MikrotikExporter.Configuration
{
    public enum MetricType
    {
        Counter,
        Gauge
    }

    public class Metric : Param
    {
        /// <summary>
        /// Use different name for metric than parameter name
        /// </summary>
        [YamlMember(Alias = "metric_name")]
        public string MetricName { get; protected set; }

        /// <summary>
        /// Prometheus Metric Type
        /// </summary>
        [Required]
        [YamlMember(Alias = "metric_type")]
        public MetricType MetricType { get; protected set; }

        [YamlMember(Alias = "help")]
        public string Help { get; protected set; }

        /// <summary>
        /// Labels specific to this Metric
        /// </summary>
        [YamlMember(Alias = "labels")]
        public List<Label> Labels { get; protected set; } = new List<Label>();

        [Required]
        [RegularExpression("^[a-zA-Z_:][a-zA-Z0-9_:]*$")]
        [YamlIgnore]
        public string MetricNameOrName => MetricName ?? Name.Replace("-", "_", System.StringComparison.InvariantCulture);
    }
}
