﻿using Mono.Options;
using Prometheus;
using Prometheus.BlackboxMetricServer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.NodeDeserializers;

namespace MikrotikExporter
{

    class Program
    {
        public static Configuration.Root Configuration { get; internal set; }
        /// <summary>
        /// full path to the configuration file
        /// </summary>
        public static string configurationFile;
        private static long requestCounter = 0;
        private static readonly CancellationTokenSource cts = new CancellationTokenSource();

        private static BlackboxMetricServer metricServer;
        private static HttpListener discoverServer;
        private static Task discoverTask;
        private static HttpListener reloadServer;
        private static Task reloadTask;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
        static void Main(string[] args)
        {
            try
            {
                bool showHelp = false;

                OptionSet optionSet = new OptionSet {
                    { "c|config=", "path to the yml configuration", c => configurationFile = Path.GetFullPath(c) },
                    { "v", "enable verbose output", v => Log.PrintDebug1 = v != null},
                    { "vv", "enable more verbose output", v => Log.PrintDebug2 = v != null},
                    { "h|help", "show this help", h => showHelp = h != null }
                };

                optionSet.Parse(args);

                Console.WriteLine($"MikrotikExporter.Net {Assembly.GetExecutingAssembly().GetName().Version}");

                if (showHelp)
                {
                    optionSet.WriteOptionDescriptions(Console.Out);
                    return;
                }

                if (string.IsNullOrWhiteSpace(configurationFile))
                {
                    Log.Main.Error("configuration file missing. use --help for more information.");
                    return;
                }

                //change current folder, so relative module paths work correctly
                var configurationFolder = Path.GetDirectoryName(configurationFile);
                Directory.SetCurrentDirectory(configurationFolder);

                // inital configuration load
                if (!ConfigurationManager.Load(Log.Main.CreateContext("configuration load initial")))
                {
                    return;
                }

                if (!string.IsNullOrEmpty(Configuration.Global.ReloadUrl))
                {
                    Log.Main.Info("start reload server");
                    reloadServer = new HttpListener();
                    reloadServer.Prefixes.Add($"http://+:{Configuration.Global.Port}/{Configuration.Global.ReloadUrl}");
                    reloadServer.Start();
                    reloadTask = Task.Factory.StartNew(delegate
                      {
                          try
                          {
                              while (!cts.Token.IsCancellationRequested)
                              {
                                  var getContext = reloadServer.GetContextAsync();
                                  getContext.Wait(cts.Token);

                                  var context = getContext.Result;
                                  _ = Task.Factory.StartNew(delegate
                                  {
                                      var response = context.Response;

                                      if (context.Request.HttpMethod != "POST")
                                      {
                                          response.StatusCode = 405;
                                          response.Close();
                                          return;
                                      }

                                      if (ConfigurationManager.Load(Log.Main.CreateContext("configuration load api")))
                                      {
                                          response.StatusCode = 200;
                                      }
                                      else
                                      {
                                          response.StatusCode = 400;
                                      }

                                      response.Close();
                                  }, cts.Token, TaskCreationOptions.None, TaskScheduler.Default);
                              }
                          }
                          catch (OperationCanceledException)
                          {
                              // ignore this exception, can be fired on shutdown
                          }
                          finally
                          {
                              Log.Main.Info("stop reload server");
                              reloadServer.Stop();
                              reloadServer.Close();
                          }
                      }, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                }

                if (!string.IsNullOrEmpty(Configuration.Global.DiscoverUrl))
                {
                    Log.Main.Info("start discover server");
                    discoverServer = new HttpListener();
                    discoverServer.Prefixes.Add($"http://+:{Configuration.Global.Port}/{Configuration.Global.DiscoverUrl}");
                    discoverServer.Start();
                    discoverTask = Task.Factory.StartNew(delegate
                    {
                        try
                        {
                            while (!cts.Token.IsCancellationRequested)
                            {
                                var getContext = discoverServer.GetContextAsync();
                                getContext.Wait(cts.Token);

                                var context = getContext.Result;
                                _ = Task.Factory.StartNew(delegate
                                {
                                    var response = context.Response;

                                    var staticConfigs = Configuration.Targets.Select((kvp) => new Discover.StaticConfig() { Labels = kvp.Value.DiscoverLabels, Targets = new string[] { kvp.Key } });

                                    var serializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
                                    response.OutputStream.Write(System.Text.Encoding.UTF8.GetBytes(serializer.Serialize(staticConfigs)));

                                    response.OutputStream.Flush();

                                    response.Close();
                                }, cts.Token, TaskCreationOptions.None, TaskScheduler.Default);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // ignore this exception, can be fired on shutdown
                        }
                        finally
                        {
                            Log.Main.Info("stop discover server");
                            discoverServer.Stop();
                            discoverServer.Close();
                        }
                    }, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                }

                // start the cleanup for stale connections
                ConnectionManager.InitCleanup(cts.Token);
                ConfigurationManager.InitReload(cts.Token);

                Log.Main.Info("start metric server");
                metricServer = new BlackboxMetricServer(Configuration.Global.Port, Configuration.Global.MetricsUrl);
                metricServer.Start();

                metricServer.AddScrapeCallback(async (cancel, factory, queryStrings) =>
                {
                    var requestId = Interlocked.Increment(ref requestCounter);
                    var log = Log.Main.CreateContext($"request {requestId}");
                    // create a reference to the currently loaded configuration, to avoid changes during a scrape
                    var localConfiguration = Configuration;

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
                        throw;
                    }
                    finally
                    {
                        log.Debug1("end scrape");
                    }
                });

                var tcsRun = new TaskCompletionSource<object>();

                Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
                {
                    Log.Main.Info("stopping...");

                    var counter = Interlocked.Read(ref requestCounter);
                    Log.Main.Info($"served {counter} requests");

                    tcsRun.SetResult(null);
                    e.Cancel = true;
                };

                tcsRun.Task.ContinueWith((action) =>
                {
                    cts.Cancel();
                    Log.Main.Info("stop metric server");
                    return Task.WhenAll(metricServer.StopAsync(), discoverTask, reloadTask);
                }, TaskScheduler.Current).Wait();
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log.Main.Error($"unexpected error: {ex}");
            }
        }


    }

}
