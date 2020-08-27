# configuration specs
## main configuration file
```yaml
global:
  # default username for connections to targets, used if not overwritten in target
  [ username: <string> | default = null ]
  # default password for connections to targets, used if not overwritten in target
  [ password: <string> | default = null ]
  
  [ port: <int> | default = 9436 ]
  [ metrics_url: <string> | default = metrics/ ]
  [ discover_url: <string | default = discover/ ]
  [ reload_url: <string> | default = -/reload/ ]
  
  # prefix for all metric names
  [ prefix: <string> | default = mikrotik ]
  
  # list of glob filepaths loaded, see next chapter
  sub_configs:
    [ - <string> ... ]

  # a scrape is aborted if the execution of a command and parsing its values takes longer than this timespan
  [ command_timeout: <timespan> | default = 00:00:05 ]
  # if a connection was successfully used in this timespan, skip checking the connection before use
  [ connection_check_interval: <timespan> | default = 00:05:00 ]
  # close a connection if not used for longer than this timespan
  [ connection_use_timeout: <timespan> | default = 00:01:00 ]
  # reload the configuration in this interval
  [ configuration_reload_interval: <timespan> | default = 00:01:00 ]

targets:
  # name of the target must be unique
  [ <string>: <target> ]

modules:
  # name of the module must be unique
  [ <string>: <module> ]
module_extensions:
  [ <string>: <module_extension> ]
```

## sub config file
```yaml
targets:
  # name of the target must be unique
  [ <string>: <target> ]

modules:
  # name of the module must be unique
  [ <string>: <module> ]
module_extensions:
  [ <string>: <module_extension> ]
```

## `<target>`
```yaml
# IP or hostname of the target
host: <string>
# username for connection to target, overwrites global.username
[ username: <string> | default = null ]
# password for connection to target, overwrites global.password
[ password: <string> | default = null ]
# labels returned at the discovery endpoint
discover_labels:
[ <string>: <string> ]
variables:
[ <string>: <string> ]
# list of modules which are executed if ?module=xxx is omitted in the scrape request
modules:
[ - <string> ... ]
# target specific module extensions
module_extensions:
[ <module_extension> ... ]
```

## `<module>`
```yaml
# a module can be comprised of multiple commands
[ - <module_command> ... ]
```

## `<module_command>`
```yaml
# command executed against the MikroTik API
# the command can be substituted with variables, using the following syntax: {name_of_variable}
command: <string>
# overwrites global.command_timeout
[ command_time: <timespan> ]
[ prefix: <string> | default = <name of the module>]
variables:
  [ - <label> ... ]
lables:
  [ - <label> ... ]
metrics:
  [ - <metric> ... ]
sub_commands:
  [ - <module_command> ... ]
```

## `<param>`
```yaml
# name of the parameter in the MikroTik API
[ name: <string> ]
# this value is used if the parameter is not found in the API response
# the value can be substituted with variables, using the following syntax: {name_of_variable}
[ default: <string> ]
# if this value is set, it is used instead of the API response -> static parameter
# the value can be substituted with variables, using the following syntax: {name_of_variable}
[ value: <string> ]

# remapping is stopped after the first match in remap_values or remap_values_re
# 1:1 remapping of values
remap_values:
  [ <string>: <string> ]
# use regex to substitute value (see: https://docs.microsoft.com/en-us/dotnet/standard/base-types/substitutions-in-regular-expressions?redirectedfrom=MSDN)
remap_values_re:
  [ - <regex>: <string> ]

# only relevant for param_type=bool
[ negate: <bool> ]

# only relevant for param_type=datetime
[ datetime_type: tonow, fromnow ]
```

## `<label>`
```yaml
# derives from param
<param>
[ param_type: string, int, bool, timespan, datetime | default = int ]
# either this or param.name must be set
[ label_name: <string> | default = <param.name> ]
```

## `<metric>`
```yaml
# derives from param
<param>
[ param_type: int, bool, timespan, datetime | default = int ]
# either this or param.name must be set
# the full name is then built with this logic: <global.prefix>_<module.prefix OR name of the module>_<metric_name OR param.name>
[ metric_name: <string> | default = <param.name> ]
[ metric_type: counter, gauge ]
[ help: <string> ]
# labels specific to this metric
labels:
  [ - <label> ... ]
```

## `<module_extension>`
same as `<module>` but uses the following `<label_extension>` and `<metric_extension>` instead of `<label>` and `<metric>` inside its commands

## `<label_extension>`
```yaml
# extends label
<label>
extension_action: add, overwrite, remove
```
## `<metric_extension>`
```yaml
# extends metric
<metric>
extension_action: add, overwrite, remove
```

# parameter flow
![mermaid-diagram-20200827213412](https://user-images.githubusercontent.com/3697291/91486859-240c6600-e8ad-11ea-95b6-2a751952f3e6.png)
