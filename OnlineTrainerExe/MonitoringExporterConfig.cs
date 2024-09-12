// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.DecisionService.OnlineTrainer;

public class MonitoringExporterConfig
{
    public bool ConsoleTraceExporterEnabled { get; set; } = true;
    public bool ConsoleLogExporterEnabled { get; set; } = true;
    public bool ConsoleMetricExporterEnabled { get; set; } = true;
    public bool OtlpTraceExporterEnabled { get; set; } = false;
    public bool OtlpLogExporterEnabled { get; set; } = false;
    public bool OtlpMetricExporterEnabled { get; set; } = false;
    public bool GenevaTraceExporterEnabled { get; set; } = false;
    public bool GenevaLogExporterEnabled { get; set; } = false;
    public bool GenevaMetricExporterEnabled { get; set; } = false;
}