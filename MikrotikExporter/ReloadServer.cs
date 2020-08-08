using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MikrotikExporter
{
    class ReloadServer
    {
        private static HttpListener listener = new HttpListener();
        internal static Task Start(CancellationToken cancellationToken)
        {
            Log.Main.Info("start reload server");
            listener.Prefixes.Add($"http://+:{Program.Configuration.Global.Port}/{Program.Configuration.Global.ReloadUrl}");
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
                        }, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
                    }
                }
                catch (OperationCanceledException)
                {
                    // ignore this exception, can be fired on shutdown
                }
                finally
                {
                    Log.Main.Info("stop reload server");
                    listener.Stop();
                    listener.Close();
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }
}
