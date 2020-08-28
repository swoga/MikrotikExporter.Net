using Mono.Options;
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
        private static readonly CancellationTokenSource cts = new CancellationTokenSource();

        private static BlackboxMetricServer metricServer;
        private static List<Task> backgroundTasks = new List<Task>();

        static void Main(string[] args)
        {
            try
            {
                bool showHelp = false;

                OptionSet optionSet = new OptionSet {
                    { "c|config=", "path to the yml configuration", c => configurationFile = Path.GetFullPath(c) },
                    { "v", "enable verbose output", v => { if (v != null) { Log.Main.Level = Log.LogLevel.Debug1; } } },
                    { "vv", "enable more verbose output", v => { if (v != null) { Log.Main.Level = Log.LogLevel.Debug2; } } },
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

                // inital configuration load
                if (!ConfigurationManager.Load(Log.Main.CreateContext("configuration load initial")))
                {
                    return;
                }

                if (!string.IsNullOrEmpty(Configuration.Global.ReloadUrl))
                {
                    backgroundTasks.Add(ReloadServer.Start(cts.Token));
                }

                if (!string.IsNullOrEmpty(Configuration.Global.DiscoverUrl))
                {
                    backgroundTasks.Add(DiscoverServer.Start(cts.Token));
                }

                // start the cleanup for stale connections
                ConnectionManager.InitCleanup(cts.Token);
                ConfigurationManager.InitReload(cts.Token);

                metricServer = MetricServer.Start();

                var tcsRun = new TaskCompletionSource<object>();

                Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
                {
                    Log.Main.Info("stopping...");
                    tcsRun.SetResult(null);
                    e.Cancel = true;
                };

                tcsRun.Task.ContinueWith((action) =>
                {
                    cts.Cancel();
                    Log.Main.Info("stop metric server");
                    backgroundTasks.Add(metricServer.StopAsync());
                    return Task.WhenAll(backgroundTasks);
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
