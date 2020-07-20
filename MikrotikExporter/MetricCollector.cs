using Prometheus;
using System;

namespace MikrotikExporter
{
    class MetricCollector
    {
        public Configuration.Metric Metric { get; private set; }
        public Func<Collector> GetOrAddCollector { get; private set; }

        public MetricCollector(Configuration.Metric metric, Func<Collector> getOrAddCollector) {
            Metric = metric;
            GetOrAddCollector = getOrAddCollector;
        }
    }
}
