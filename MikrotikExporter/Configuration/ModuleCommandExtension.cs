using System;
using System.Collections.Generic;
using System.Linq;

namespace MikrotikExporter.Configuration
{
    public class ModuleCommandExtension : ModuleCommandBase<LabelExtension, MetricExtension, ModuleCommandExtension>
    {
        internal bool TryExtendModuleCommand(Log log, ModuleCommand moduleCommand)
        {
            var success = true;

            ExtendLabelOrVariables(log.CreateContext("lables"), Labels, moduleCommand.Labels);
            ExtendLabelOrVariables(log.CreateContext("variables"), Variables, moduleCommand.Variables);
            ExtendMetrics(log.CreateContext("metrics"), Metrics, moduleCommand.Metrics);

            return success;
        }

        private static void ExtendLabelOrVariables(Log log, List<LabelExtension> extensions, List<Label> originals)
        {
            foreach (var labelExtension in extensions)
            {
                if (labelExtension.ExtensionAction == ExtensionEnum.Add)
                {
                    log.Debug1($"{labelExtension.ExtensionAction} item '{labelExtension.LabelNameOrName}'");
                    originals.Add(labelExtension);
                }
                else
                {
                    //search all labels/variables with the same name (normally this should be only one)
                    var labelsWithIndex = originals.Select((label, index) => new { label, index }).Where((labelWithIndex) => labelWithIndex.label.LabelNameOrName == labelExtension.LabelNameOrName).ToArray();

                    if (labelsWithIndex.Length == 0)
                    {
                        log.Info($"no item named '{labelExtension.LabelNameOrName}' found for action {labelExtension.ExtensionAction}");
                    }

                    foreach (var labelWithIndex in labelsWithIndex)
                    {
                        log.Debug1($"{labelExtension.ExtensionAction} item '{labelExtension.LabelNameOrName}'");
                        switch (labelExtension.ExtensionAction)
                        {
                            case ExtensionEnum.Overwrite:
                                originals[labelWithIndex.index] = labelExtension;
                                break;
                            case ExtensionEnum.Remove:
                                originals.Remove(labelWithIndex.label);
                                break;
                            default:
                                throw new NotImplementedException();
                        }
                    }
                }
            }
        }

        private static void ExtendMetrics(Log log, List<MetricExtension> extensions, List<Metric> originals)
        {
            foreach (var metricExtension in extensions)
            {
                if (metricExtension.ExtensionAction == ExtensionEnum.Add)
                {
                    log.Debug1($"{metricExtension.ExtensionAction} item '{metricExtension.MetricNameOrName}'");
                    originals.Add(metricExtension);
                }
                else
                {
                    //search all metrics with the same name (normally this should be only one)
                    var metricsWithIndex = originals.Select((metric, index) => new { metric, index }).Where((metricWithIndex) => metricWithIndex.metric.MetricNameOrName == metricExtension.MetricNameOrName).ToArray();

                    if (metricsWithIndex.Length == 0)
                    {
                        log.Info($"no item named '{metricExtension.MetricNameOrName}' found for action {metricExtension.ExtensionAction}");
                    }

                    foreach (var metricWithIndex in metricsWithIndex)
                    {
                        log.Debug1($"{metricExtension.ExtensionAction} item '{metricExtension.MetricNameOrName}'");
                        switch (metricExtension.ExtensionAction)
                        {
                            case ExtensionEnum.Overwrite:
                                originals[metricWithIndex.index] = metricExtension;
                                break;
                            case ExtensionEnum.Remove:
                                originals.Remove(metricWithIndex.metric);
                                break;
                            default:
                                throw new NotImplementedException();
                        }
                    }
                }
            }
        }


    }
}
