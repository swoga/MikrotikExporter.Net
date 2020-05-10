using System;
using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using tik4net;

namespace MikrotikExporter
{
    static class ConnectionManager
    {
        private static readonly ConcurrentDictionary<string, Connection> connections = new ConcurrentDictionary<string, Connection>();

        public class Connection
        {
            private DateTime lastUse = DateTime.Now;
            private readonly object lastUseLock = new object();
            public DateTime LastUse
            {
                get
                {
                    lock(lastUseLock)
                    {
                        return lastUse;
                    }
                }
                set
                {
                    lock(lastUseLock)
                    {
                        lastUse = value;
                    }
                }
            }
            public ITikConnection TikConnection { get; }

            public Connection(string host, string user, string pass)
            {
                TikConnection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
                TikConnection.SendTagWithSyncCommand = true;
                TikConnection.Open(host, user, pass);
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
            public Task<bool> Check(Log log)
            {
                if (!TikConnection.IsOpened)
                {
                    log.Debug1("connection is not open");
                    return Task.FromResult(false);
                }

                // do not send test command, if last use was recently
                if ((DateTime.Now - LastUse) < Program.Configuration.Global.ConnectionCheckInterval)
                {
                    log.Debug1("connection was used recently, do not test");
                    return Task.FromResult(true);
                }
                else
                {
                    try
                    {
                        TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

                        var command = TikConnection.CreateCommand("/system/identity/print");
                        command.ExecuteAsync(delegate
                        {
                            log.Debug1("connection test successful");
                            tcs.SetResult(true);
                        }, delegate
                        {
                            log.Debug1("connection test unsuccessful");
                            tcs.SetResult(false);
                        });

                        return tcs.Task;
                    }
                    catch
                    {
                        log.Debug1("exception on connection check");
                        return Task.FromResult(false);
                    }
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
        private static void Cleanup()
        {
            var log = Log.Main.CreateContext("cleanup");

            log.Debug1("start");

            foreach (var kvp in connections)
            {
                if (DateTime.Now - kvp.Value.LastUse > Program.Configuration.Global.ConnectionUseTimeout)
                {
                    log.Debug1($"remove connection {kvp.Key}");

                    kvp.Value.TikConnection.Close();
                    connections.TryRemove(kvp.Key, out _);
                }
            }
        }

        public static Task InitCleanup(CancellationToken token)
        {
            return Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    Cleanup();
                    await Task.Delay(Program.Configuration.Global.ConnectionCleanupInterval, token).ConfigureAwait(false);
                }
            }, token);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2002:Do not lock on objects with weak identity", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
        public static Connection GetConnection(Log log, string host, string user, string pass)
        {
            string key = $"{host};{user};{pass}";
            var cmLogger = log.CreateContext($"connection {key}");

            // lock on the target host and credentials, to ensure only one connection to the target is opened
            lock (string.Intern(key))
            {
                connections.TryGetValue(key, out Connection connection);

                if (connection != null)
                {
                    cmLogger.Debug1($"got connection from cache");

                    lock (connection)
                    {
                        if (connection.Check(cmLogger).Result)
                        {
                            return connection;
                        }
                    }
                }

                cmLogger.Debug1($"open connection");
                connection = new Connection(host, user, pass);
                connections.AddOrUpdate(key, connection, (key, existingConnection) =>
                {
                    cmLogger.Debug2($"update connection {key} in cache, was added in the meantime");
                    return connection;
                });

                return connection;
            }
        }
    }
}
