using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MikrotikExporter
{
    class DiscoverServer
    {
        private static HttpListener listener = new HttpListener();
        internal static Task Start(CancellationToken cancellationToken)
        {
            Log.Main.Info("start discover server");
            listener.Prefixes.Add($"http://+:{Program.Configuration.Global.Port}/{Program.Configuration.Global.DiscoverUrl}");
            listener.Start();
            return Task.Factory.StartNew(delegate
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var getContext = listener.GetContextAsync();
                        getContext.Wait(cancellationToken);

                        var context = getContext.Result;
                        _ = Task.Factory.StartNew(delegate
                        {
                            var response = context.Response;

                            var staticConfigs = Program.Configuration.Targets.Select((kvp) => new Discover.StaticConfig() { Labels = kvp.Value.DiscoverLabels, Targets = new string[] { kvp.Key } });

                            var serializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
                            response.OutputStream.Write(System.Text.Encoding.UTF8.GetBytes(serializer.Serialize(staticConfigs)));

                            response.OutputStream.Flush();

                            response.Close();
                        }, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
                    }
                }
                catch (OperationCanceledException)
                {
                    // ignore this exception, can be fired on shutdown
                }
                finally
                {
                    Log.Main.Info("stop discover server");
                    listener.Stop();
                    listener.Close();
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }
}
