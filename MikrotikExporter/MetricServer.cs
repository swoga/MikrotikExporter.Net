﻿using Force.DeepCloner;
using Prometheus;
using Prometheus.BlackboxMetricServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MikrotikExporter
{
    class MetricServer
    {
        private static long requestCounter = 0;
        static internal BlackboxMetricServer Start()
        {
            Log.Main.Info("start metric server");
            var metricServer = new BlackboxMetricServer(Program.Configuration.Global.Port, Program.Configuration.Global.MetricsUrl);
            metricServer.Start();

            metricServer.AddScrapeCallback(async (cancel, factory, queryStrings) =>
            {
                var requestId = Interlocked.Increment(ref requestCounter);

                bool showLogOutput = false;
                Log.LogLevel? logLevel = null;
                bool invalidLogLevel = false;
                string logLevelStr = queryStrings["debug"];

                if (logLevelStr != null)
                {
                    showLogOutput = true;

                    if (Enum.TryParse(typeof(Log.LogLevel), logLevelStr, out object logLevelObj))
                    {
                        logLevel = (Log.LogLevel)logLevelObj;
                    }
                    else
                    {
                        invalidLogLevel = true;
                    }
                }

                var log = Log.Main.CreateContext($"request {requestId}", logLevel, showLogOutput);
                if (invalidLogLevel)
                {
                    log.Error($"invalid log level '{logLevelStr}' set");
                }

                // create a reference to the currently loaded configuration, to avoid changes during a scrape
                var localConfiguration = Program.Configuration;

                log.Debug1("start scrape");

                try
                {
                    var target = queryStrings["target"];

                    if (target == null)
                    {
                        throw new ScrapeFailedException("target missing");
                    }

                    if (!localConfiguration.Targets.TryGetValue(target, out var targetConfiguration))
                    {
                        throw new ScrapeFailedException($"target '{target}' not found");
                    }

                    string username = targetConfiguration.Username ?? localConfiguration.Global.Username;
                    string password = targetConfiguration.Password ?? localConfiguration.Global.Password;

                    var connection = ConnectionManager.GetConnection(log, targetConfiguration.Host, username, password);

                    string[] moduleNames;

                    // either use modules listed in query string or the modules listed in target
                    var queryModule = queryStrings["module"];
                    if (queryModule != null)
                    {
                        moduleNames = queryModule.Split(",");
                    }
                    else
                    {
                        moduleNames = targetConfiguration.Modules;
                    }

                    await Task.WhenAll(moduleNames.Select((moduleName) =>
                    {
                        if (!localConfiguration.Modules.TryGetValue(moduleName, out var module))
                        {
                            throw new ScrapeFailedException($"module '{moduleName}' not found");
                        }

                        var moduleLogger = log.CreateContext($"module {moduleName}");

                        if (targetConfiguration.ModuleExtensions.TryGetValue(moduleName, out var moduleExtension))
                        {
                            moduleLogger.Debug1("target has module extension");

                            module = module.DeepClone();
                            moduleExtension.TryExtendModule(log.CreateContext("target extension"), module);
                        }

                        var tasks = new List<Task>();
                        var iCommand = 1;
                        foreach (var moduleCommand in module)
                        {
                            var commandLogger = moduleLogger.CreateContext($"command {iCommand++}");
                            var namePrefix = localConfiguration.Global.Prefix + '_' + (moduleCommand.Prefix ?? moduleName) + '_';


                            var metricCollectorsCache = new Dictionary<Configuration.ModuleCommand, MetricCollector[]>();
                            moduleCommand.Prepare(commandLogger, factory, namePrefix, metricCollectorsCache);

                            tasks.Add(moduleCommand.Run(commandLogger, connection.TikConnection, factory, localConfiguration, namePrefix, targetConfiguration.Variables, metricCollectorsCache));
                        }

                        return Task.WhenAll(tasks);
                    })).ConfigureAwait(false);

                    connection.LastUse = DateTime.Now;
                }
                catch (Exception ex)
                {
                    log.Error(ex.ToString());
                    if (!showLogOutput)
                    {
                        throw;
                    }
                }
                finally
                {
                    log.Debug1("end scrape");
                }

                return showLogOutput ? string.Join("\r\n", log.Logs) : null;
            });

            return metricServer;
        }
    }
}
