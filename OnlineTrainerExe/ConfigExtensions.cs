// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter.Geneva;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.DecisionService.OnlineTrainer
{
    public static class ConfigurationBuilderExtensions
    {
        // Optional, if KeyVault argument is not provided, no keyvault will be used for secrets.
        public static IConfigurationBuilder AddAzureKeyVault(
            this IConfigurationBuilder builder)
        {
            var tempConfig = builder.Build();
            var settings = tempConfig.Get<KeyVaultConfig>();
            if (string.IsNullOrEmpty(settings?.KeyVault))
            {
                return builder;
            }

            var vaultUriString = Uri.IsWellFormedUriString(settings.KeyVault, UriKind.Absolute)
                ? settings.KeyVault
                : $"https://{settings.KeyVault}.vault.azure.net/";

            var options = new AzureKeyVaultConfigurationOptions
            {
                ReloadInterval = settings.KeyVaultReloadInterval
            };

            return builder.AddAzureKeyVault(new Uri(vaultUriString), new DefaultAzureCredential(), options);
        }

        public class PrefixKeyVaultSecretManager : KeyVaultSecretManager
        {
            private readonly string _prefix;

            public PrefixKeyVaultSecretManager(string prefix)
                => _prefix = $"{prefix}{Constants.PREFIX_DELIMITER}";

            public override bool Load(SecretProperties properties)
                => properties.Name.StartsWith(_prefix);

            public override string GetKey(KeyVaultSecret secret)
                => secret.Name[_prefix.Length..].Replace("--", ConfigurationPath.KeyDelimiter);
        }

        public static IConfigurationBuilder AddPrefixedAzureKeyVault(
            this IConfigurationBuilder builder, string prefix)
        {
            var tempConfig = builder.Build();
            var settings = tempConfig.Get<KeyVaultConfig>();
            if (string.IsNullOrEmpty(settings?.KeyVault))
            {
                return builder;
            }

            var vaultUriString = Uri.IsWellFormedUriString(settings.KeyVault, UriKind.Absolute)
                ? settings.KeyVault
                : $"https://{settings.KeyVault}.vault.azure.net/";

            var options = new AzureKeyVaultConfigurationOptions
            {
                ReloadInterval = settings.KeyVaultReloadInterval,
                Manager = new PrefixKeyVaultSecretManager(prefix)
            };
            return builder.AddAzureKeyVault(new Uri(vaultUriString), new DefaultAzureCredential(), options);
        }

        public static IConfigurationBuilder AddTableStorage(
            this IConfigurationBuilder builder, string rowKey)
        {
            var tempConfig = builder.Build();
            var tableStorageConfig = tempConfig.Get<TableStorageConfig>();
            if (tableStorageConfig.TableStorageEndpoint == null)
            {
                return builder;
            }

            // TODO: Use real logger etc
            return builder.Add(new TableStorageConfigSource(tableStorageConfig, rowKey));
        }

        private static string GetGenevaConnectionString()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "Endpoint=unix:/var/run/mdsd/default_fluent.socket";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                throw new PlatformNotSupportedException("Geneva exporter is not supported on OSX.");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "EtwSession=OpenTelemetry";
            }
            else
            {
                throw new PlatformNotSupportedException("Geneva exporter is not supported on this platform.");
            }
        }

        public static ILoggingBuilder AddLogging(
            this ILoggingBuilder builder, MonitoringExporterConfig config)
        {
            return builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.IncludeFormattedMessage = true;

                if (config.ConsoleLogExporterEnabled)
                {
                    options.AddConsoleExporter();
                }

                if (config.OtlpLogExporterEnabled)
                {
                    options.AddOtlpExporter();
                }

                if (config.GenevaLogExporterEnabled)
                {
                    options.AddGenevaLogExporter(exporterOptions =>
                    {
                        exporterOptions.ConnectionString = GetGenevaConnectionString();
                    });
                }
            });
        }

        public static IServiceCollection AddTracing(
            this IServiceCollection builder, MonitoringExporterConfig config)
        {
            builder.AddOpenTelemetry().ConfigureResource(
                resource =>
                    resource.AddService(serviceName: "OnlineTrainer")).WithTracing(options =>
            {
                options.AddSource("Microsoft.DecisionService.OnlineTrainer");
                options.AddSource("Trainer.*");
                if (config.ConsoleTraceExporterEnabled)
                {
                    options.AddConsoleExporter();
                }

                if (config.OtlpTraceExporterEnabled)
                {
                    options.AddOtlpExporter();
                }

                if (config.GenevaTraceExporterEnabled)
                {
                    options.AddGenevaTraceExporter(exporterOptions =>
                    {
                        exporterOptions.ConnectionString = GetGenevaConnectionString();
                    });
                }
            });

            return builder;
        }

        public static IServiceCollection AddMetrics(
            this IServiceCollection builder, MonitoringExporterConfig config)
        {
            builder.AddOpenTelemetry()
                .ConfigureResource(
                    resource =>
                        resource.AddService(serviceName: "OnlineTrainer"))
                .WithMetrics(
                    metricsBuilder =>
                    {
                        metricsBuilder.AddMeter("Microsoft.DecisionService.OnlineTrainer*");

                        if (config.ConsoleMetricExporterEnabled)
                        {
                            metricsBuilder.AddConsoleExporter();
                        }

                        if (config.OtlpMetricExporterEnabled)
                        {
                            metricsBuilder.AddOtlpExporter();
                        }

                        if (config.GenevaMetricExporterEnabled)
                        {
                            metricsBuilder.AddGenevaMetricExporter(exporterOptions =>
                            {
                                exporterOptions.ConnectionString = GetGenevaConnectionString();
                            });
                        }
                    });

            return builder;
        }
    }
}