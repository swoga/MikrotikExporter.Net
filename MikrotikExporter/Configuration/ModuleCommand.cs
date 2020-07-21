using Prometheus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using tik4net;
using YamlDotNet.Serialization;

namespace MikrotikExporter.Configuration
{
    public class ModuleCommand
    {
        [YamlMember(Alias = "command")]
        public string Command { get; private set; }

        [YamlMember(Alias = "command_timeout")]
        public TimeSpan? CommandTimeout { get; set; }

        [YamlMember(Alias = "prefix")]
        public string Prefix { get; private set; }

        [YamlMember(Alias = "labels")]
        public List<Label> Labels { get; private set; } = new List<Label>();

        [YamlMember(Alias = "metrics")]
        public List<Metric> Metrics { get; private set; } = new List<Metric>();

        internal async Task Run(Log log, ITikConnection connection, MetricFactory metricFactory, Configuration.Root configuration, string moduleName)
        {
            log.Debug1($"run command '{Command}'");

            var tcs = new TaskCompletionSource<object>();

            var moduleLabelNames = Labels.Select(label => label.LabelNameOrName).ToArray();
            var namePrefix = configuration.Global.Prefix + '_' + (Prefix ?? moduleName) + '_';

            var metricCollectors = Metrics.Select((metric) =>
            {
                var metricLabelNames = metric.Labels.Select(label => label.LabelNameOrName);
                var labelNames = moduleLabelNames.Concat(metricLabelNames).ToArray();
                var metricNameWithPrefix = namePrefix + metric.MetricNameOrName;

                log.Debug2($"prepare metric '{metricNameWithPrefix}' as {metric.MetricType}");

                return metric.MetricType switch
                {
                    MetricType.Gauge => new MetricCollector(metric, () => metricFactory.CreateGauge(metricNameWithPrefix, metric.Help, labelNames)),
                    MetricType.Counter => new MetricCollector(metric, () => metricFactory.CreateCounter(metricNameWithPrefix, metric.Help, labelNames)),
                    _ => throw new Exception("unkown type"),
                };
            }).ToArray();

            var tikCommand = connection.CreateCommand(Command);
            int responseCounter = 0;
            var ctsTimeout = new CancellationTokenSource();

            try
            {
                // if the command is not completed after a timeout period, throw an exception
                var timeout = CommandTimeout ?? configuration.Global.CommandTimeout;
                _ = Task.Delay(timeout, ctsTimeout.Token).ContinueWith((_) => tcs.TrySetException(new ScrapeFailedException("command not completed after timeout period")), ctsTimeout.Token, TaskContinuationOptions.None, TaskScheduler.Default);

                tikCommand.ExecuteAsync((re) =>
                {
                    if (tcs.Task.IsFaulted)
                    {
                        log.Error("ignore api response, scrape already faulted");
                        return;
                    }

                    var responseId = Interlocked.Increment(ref responseCounter);
                    var responseLogger = log.CreateContext($"response {responseId}");
                    responseLogger.Debug2($"api response: {re}");

                    foreach (var metricCollector in metricCollectors)
                    {
                        var metric = metricCollector.Metric;
                        var metricLogger = responseLogger.CreateContext($"metric {metric.MetricNameOrName}");

                        if (metric.TryGetValue(metricLogger, re, out var value))
                        {
                            // get or add collector only if a value can be determined, either from the response or from the default value
                            var collector = metricCollector.GetOrAddCollector();

                            metricLogger.Debug2($"got value '{value}', create metric");
                            var labelLogger = metricLogger.CreateContext("labels");
                            var moduleLabelValues = Labels.Select(label => label.AsString(labelLogger, re));
                            var metricLabelValues = metric.Labels.Select(label => label.AsString(labelLogger, re));
                            var labelValues = moduleLabelValues.Concat(metricLabelValues).ToArray();

                            switch (collector)
                            {
                                case Gauge gauge:
                                    gauge.WithLabels(labelValues).Set(value);
                                    break;
                                case Counter counter:
                                    counter.WithLabels(labelValues).IncTo(value);
                                    break;
                            }
                        }
                    }
                },
                (trap) => tcs.TrySetException(new ScrapeFailedException(trap.ToString())),
                () =>
                {
                    log.Debug1("done callback");
                    tcs.TrySetResult(null);
                }
                );

                await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                ctsTimeout.Cancel();
                ctsTimeout.Dispose();
            }
        }
    }
}
