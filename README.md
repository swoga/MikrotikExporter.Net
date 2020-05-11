# Prometheus Exporter for MikroTik devices
Prometheus Exporter written in .NET Core 3.1 to export metrics from MikroTik devices.  
It is not predetermined which metrics are collected, you can create own modules, please see the examples [here](https://github.com/swoga/MikrotikExporter.Net/blob/master/examples/example.yml) and [here](https://github.com/swoga/MikrotikExporter.Net/blob/master/examples/modules/interface.yml)

[configuration defaults](https://github.com/swoga/MikrotikExporter.Net/tree/master/MikrotikExporter/Configuration)

### start arguments
```
-c, --config=VALUE         path to the yml configuration
-v                         enable verbose output
-vv                        enable more verbose output
-h, --help                 show help
```

### endpoints
```
http://127.0.0.1:9436/metrics?target=router1                              scrapes all modules defined at the target
http://127.0.0.1:9436/metrics?target=router1&module=interface             scrapes only the interface module
http://127.0.0.1:9436/metrics?target=router1&module=interface,resource    scrapes the interface and resource module
http://127.0.0.1:9436/discover                                            returns all targets in static_config format
http://127.0.0.1:9436/reload                                              reloads all configuration files
```

## uses the following libraries
- [prometheus-net.BlackboxMetricServer](https://github.com/swoga/prometheus-net.BlackboxMetricServer)
- [tik4net](https://github.com/danikf/tik4net)


## TODO
- suppress metric if no value assigned at scrape (needed for health module)
- docker image
- split main class into seperate files
- documentation
- automated tests
