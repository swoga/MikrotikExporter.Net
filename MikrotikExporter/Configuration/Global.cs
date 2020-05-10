﻿using System;
using System.Collections.Generic;
using System.Text;
using YamlDotNet.Serialization;

namespace MikrotikExporter.Configuration
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "<Pending>")]
    public class Global
    {
        /// <summary>
        /// Default username for targets
        /// </summary>
        [YamlMember(Alias = "username")]
        public string Username { get; set; }

        /// <summary>
        /// Default password for targets
        /// </summary>
        [YamlMember(Alias = "password")]
        public string Password { get; set; }

        [YamlMember(Alias = "port")]
        public int Port { get; set; } = 9436;

        [YamlMember(Alias = "metrics_url")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:Uri properties should not be strings", Justification = "<Pending>")]
        public string MetricsUrl { get; set; } = "metrics/";

        [YamlMember(Alias = "discover_url")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:Uri properties should not be strings", Justification = "<Pending>")]
        public string DiscoverUrl { get; set; } = "discover/";

        [YamlMember(Alias = "reload_url")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:Uri properties should not be strings", Justification = "<Pending>")]
        public string ReloadUrl { get; set; } = "reload/";

        /// <summary>
        /// Prefix for metric names
        /// </summary>
        [YamlMember(Alias = "prefix")]
        public string Prefix { get; set; } = "mikrotik";

        [YamlMember(Alias = "module_folder")]
        public string ModuleFolder { get; set; }

        [YamlMember(Alias = "command_timeout")]
        public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(5);

        [YamlMember(Alias = "connection_check_interval")]
        public TimeSpan ConnectionCheckInterval { get; set; } = TimeSpan.FromMinutes(1);

        [YamlMember(Alias = "connection_use_timeout")]
        public TimeSpan ConnectionUseTimeout { get; set; } = TimeSpan.FromMinutes(5);

        [YamlMember(Alias = "connection_cleanup_interval")]
        public TimeSpan ConnectionCleanupInterval { get; set; } = TimeSpan.FromMinutes(1);

    }
}