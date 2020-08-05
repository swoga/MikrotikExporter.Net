using Mono.Options;
using Prometheus;
using System;
using System.Collections.Generic;
using System.Globalization;
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

        [YamlMember(Alias = "variables")]
        public List<Label> Variables { get; private set; } = new List<Label>();

        [YamlMember(Alias = "sub_commands")]
        public List<ModuleCommand> SubCommands { get; private set; } = new List<ModuleCommand>();

        /// <summary>
        /// Prepares all metrics for this and all subordinate commands
        /// </summary>
        /// <param name="log"></param>
        /// <param name="metricFactory"></param>
        /// <param name="namePrefix"></param>
        /// <param name="metricCollectorsCache"></param>
        internal void Prepare(Log log, MetricFactory metricFactory, string namePrefix, Dictionary<ModuleCommand, MetricCollector[]> metricCollectorsCache)
        {
            var moduleLabelNames = Labels.Select(label => label.LabelNameOrName).ToArray();

            metricCollectorsCache.Add(this, Metrics.Select((metric) =>
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
            }).ToArray());

            foreach(var command in SubCommands)
            {
                command.Prepare(log, metricFactory, namePrefix, metricCollectorsCache);
            }
        }

        internal async Task Run(Log log, ITikConnection connection, MetricFactory metricFactory, Configuration.Root configuration, string namePrefix, Dictionary<string, string> parentVariables, Dictionary<ModuleCommand, MetricCollector[]> metricCollectorsCache)
        {
            var tcs = new TaskCompletionSource<object>();

            var substitutedCommand = Command;
            foreach (var variable in parentVariables)
            {
                substitutedCommand = substitutedCommand.Replace($"{{{variable.Key}}}", variable.Value, true, CultureInfo.InvariantCulture);
            }

            log.Debug1($"run command '{substitutedCommand}'");

            var tikCommand = connection.CreateCommand(substitutedCommand);
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

                    var moduleLabelLogger = responseLogger.CreateContext("labels");
                    var moduleLabelValues = Labels.Select(label => label.AsString(moduleLabelLogger.CreateContext(label.LabelNameOrName), re, parentVariables)).ToArray();
                    var metricCollectors = metricCollectorsCache[this];
                    var metricsLogger = responseLogger.CreateContext("metrics");

                    foreach (var metricCollector in metricCollectors)
                    {
                        var metric = metricCollector.Metric;
                        var metricLogger = responseLogger.CreateContext(metric.MetricNameOrName);

                        if (metric.TryGetValue(metricLogger, re, parentVariables, out var value))
                        {
                            // get or add collector only if a value can be determined, either from the response or from the default value
                            var collector = metricCollector.GetOrAddCollector();

                            metricLogger.Debug2($"got value '{value}', create metric");
                            var labelLogger = metricLogger.CreateContext("labels");
                            var metricLabelValues = metric.Labels.Select(label => label.AsString(labelLogger.CreateContext(label.LabelNameOrName), re, parentVariables));
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

                    var childVariables = new Dictionary<string, string>(parentVariables);
                    var variablesLogger = responseLogger.CreateContext("variables");
                    foreach(var variable in Variables)
                    {
                        var name = variable.LabelNameOrName;
                        var variableLogger = variablesLogger.CreateContext(name);
                        var value = variable.AsString(variableLogger, re, parentVariables);

                        if (childVariables.TryAdd(name, value))
                        {
                            variableLogger.Debug2($"add with value {value}");
                            continue;
                        }

                        variableLogger.Debug2($"overwrite with value {value}");
                        childVariables[name] = value;
                    }

                    var iSubCommand = 1;
                    foreach (var subCommand in SubCommands)
                    {
                        var subcommandLogger = responseLogger.CreateContext($"command {iSubCommand++}");
                        subCommand.Run(subcommandLogger, connection, metricFactory, configuration, namePrefix, childVariables, metricCollectorsCache).Wait();
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
