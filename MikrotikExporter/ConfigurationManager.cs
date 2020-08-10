using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using MikrotikExporter.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MikrotikExporter
{
    class ConfigurationManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
        public static bool Load(Log log)
        {
            log.Info("load configuration");

            try
            {
                Configuration.Root newConfiguration;
                using (var streamReader = File.OpenText(Program.configurationFile))
                {
                    newConfiguration = YamlDeserializer.Parse<Configuration.Root>(streamReader);
                }

                bool error = false;

                if (newConfiguration.Global.SubConfigs != null)
                {
                    var configurationFolder = Path.GetDirectoryName(Program.configurationFile);

                    log.Debug2($"use this for relative paths: '{configurationFolder}'");

                    var matcher = new Matcher();
                    matcher.AddIncludePatterns(newConfiguration.Global.SubConfigs);
                    var matcherResult = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(configurationFolder)));

                    foreach (var subConfigFilePath in matcherResult.Files.Select(fpm => Path.GetFullPath(fpm.Path, configurationFolder)))
                    {
                        try
                        {
                            log.Debug1($"load sub config from '{subConfigFilePath}'");

                            using var streamReader = File.OpenText(subConfigFilePath);
                            var subConfig = YamlDeserializer.Parse<Configuration.SubConfig>(streamReader);

                            foreach (var kvpModule in subConfig.Modules)
                            {
                                if (newConfiguration.Modules.ContainsKey(kvpModule.Key))
                                {
                                    error = true;
                                    log.Error($"failed to add module '{kvpModule.Key}' from '{subConfigFilePath}', module already exists");
                                    continue;
                                }

                                newConfiguration.Modules.Add(kvpModule.Key, kvpModule.Value);
                            }

                            error = ParseModuleExtensions(log, newConfiguration.Modules, subConfig.ModuleExtensions) && error;

                            foreach (var kvpTarget in subConfig.Targets)
                            {
                                if (newConfiguration.Targets.ContainsKey(kvpTarget.Key))
                                {
                                    error = true;
                                    log.Error($"failed to add target '{kvpTarget.Key}' from '{subConfigFilePath}', target already exists");
                                    continue;
                                }

                                newConfiguration.Targets.Add(kvpTarget.Key, kvpTarget.Value);
                            }
                        }
                        catch (Exception ex)
                        {
                            error = true;
                            log.Error($"error loading '{subConfigFilePath}': {ex}");
                            continue;
                        }
                    }
                }

                error = ParseModuleExtensions(log, newConfiguration.Modules, newConfiguration.ModuleExtensions) && error;

                if (error)
                {
                    log.Error("abort configuration load due to previous errors");
                    return false;
                }

                // if this is a configuration reload, check if certain immutable options were changed
                if (Program.Configuration != null)
                {
                    if (Program.Configuration.Global.MetricsUrl != newConfiguration.Global.MetricsUrl)
                    {
                        log.Info("change ignored, changing the metrics_url is not allowed during runtime");
                    }

                    if (Program.Configuration.Global.DiscoverUrl != newConfiguration.Global.DiscoverUrl)
                    {
                        log.Info("change ignored, changing the discover_url is not allowed during runtime");
                    }

                    if (Program.Configuration.Global.ReloadUrl != newConfiguration.Global.ReloadUrl)
                    {
                        log.Info("change ignored, changing the reload_url is not allowed during runtime");
                    }

                    if (Program.Configuration.Global.Port != newConfiguration.Global.Port)
                    {
                        log.Info("change ignored, changing the port is not allowed during runtime");
                    }
                }

                if (error)
                {
                    return false;
                }

                Program.Configuration = newConfiguration;
                return true;
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
                return false;
            }
        }

        private static bool ParseModuleExtensions(Log log, Dictionary<string, Module> modules, Dictionary<string, ModuleExtension> moduleExtensions)
        {
            var logExtensions = log.CreateContext("extensions");

            var success = true;
            foreach (var kvpModuleExtension in moduleExtensions)
            {
                var logModuleExtension = logExtensions.CreateContext(kvpModuleExtension.Key);

                if (!modules.TryGetValue(kvpModuleExtension.Key, out var module))
                {
                    logModuleExtension.Info($"found extension, but this module was not found");
                    continue;
                }

                success = kvpModuleExtension.Value.TryExtendModule(logModuleExtension, module) && success;
            }
            return success;
        }

        public static Task InitReload(CancellationToken token)
        {
            return Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(Program.Configuration.Global.ConfigurationReloadInterval, token).ConfigureAwait(false);
                    if (!token.IsCancellationRequested)
                    {
                        Load(Log.Main.CreateContext("configuration load interval"));
                    }
                }
            }, token);
        }
    }
}
