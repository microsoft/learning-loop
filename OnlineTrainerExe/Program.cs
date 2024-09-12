// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Identity;
using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Billing;
using Microsoft.DecisionService.Common.Config;
using Microsoft.DecisionService.Common.Storage;
using Microsoft.DecisionService.Common.Storage.Azure;
using Microsoft.DecisionService.Common.Trainer;
using Microsoft.DecisionService.Common.Utils.Exe;
using Microsoft.DecisionService.OnlineTrainer.Join;
using Microsoft.DecisionService.OnlineTrainer.Operations;
using Microsoft.DecisionService.VowpalWabbit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VW.VWBinResolver;
using System;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.OnlineTrainer
{
    public class Program
    {
        static Task Main(string[] args)
        {
            return RunAsync(args);
        }

        private static async Task RunAsync(string[] args)
        {
            try
            {
                var builder = Host.CreateApplicationBuilder(args);
                builder.Configuration.AddJsonFile("appsettings.json", optional: true);
                builder.Configuration.AddJsonFile("appsettings.user.json", optional: true);
                builder.Configuration.AddCommandLine(args);
                builder.Configuration.AddEnvironmentVariables();

                builder.Services.Configure<MonitoringExporterConfig>(builder.Configuration);

                builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
                var monitoringExporterConfig = builder.Configuration.Get<MonitoringExporterConfig>();
                builder.Logging.AddLogging(monitoringExporterConfig);
                builder.Services.AddTracing(monitoringExporterConfig);
                builder.Services.AddMetrics(monitoringExporterConfig);

                builder.Configuration.AddAzureKeyVault();
                // Optional.
                var appId = builder.Configuration.Get<AppIdConfig>().AppId;
                builder.Configuration.AddTableStorage(appId);
                // Optional.
                // Config for appid specific items
                builder.Configuration.AddPrefixedAzureKeyVault(appId);

                builder.Services.AddOptions<AppIdConfig>()
                    .Bind(builder.Configuration)
                    .ValidateDataAnnotations();

                builder.Services.AddOptions<JoinerConfig>()
                    .Bind(builder.Configuration)
                    .ValidateDataAnnotations();

                builder.Services.AddOptions<TrainerConfig>()
                    .Bind(builder.Configuration)
                    .ValidateDataAnnotations();

                builder.Services.AddOptions<StorageConfig>()
                    .Bind(builder.Configuration)
                    .ValidateDataAnnotations();

                builder.Services.AddOptions<LogRetentionConfig>()
                    .Bind(builder.Configuration)
                    .ValidateDataAnnotations();

                builder.Services.AddOptions<TrainingMonitoringConfig>()
                    .Bind(builder.Configuration)
                    .ValidateDataAnnotations();

                builder.Services.AddOptions<StorageBlockOptions>()
                    .Bind(builder.Configuration)
                    .ValidateDataAnnotations();

                builder.Services.AddOptions<TableStorageConfig>()
                    .Bind(builder.Configuration.GetSection("TableStorage"))
                    .ValidateDataAnnotations();

                builder.Services.AddSingleton(typeof(LoggerWithAppId<>));
                builder.Services.AddSingleton(typeof(LoggerWithAppId));
                builder.Services.AddSingleton(typeof(ILogger), typeof(Logger<Program>));

                builder.Services.AddSingleton<ITimeProvider>(_ => SystemTimeProvider.Instance);
                builder.Services.AddSingleton<IOnlineTrainer>(p =>
                {
                    var trainerConfig = p.GetService<IOptions<TrainerConfig>>().Value;
                    IExeRunner exeRunner = new CommandLineExeRunner(trainerConfig.VwBinaryPath ?? Resolver.ResolveVwBinary());
                    return new VwRunner(trainerConfig.MachineLearningArguments, exeRunner);
                });

                builder.Services.AddSingleton<IStorageFactory, AzStorageFactory>(p => {
                    var storageConfig = p.GetService<IOptions<StorageConfig>>();
                    return new AzStorageFactory(storageConfig.Value.StorageAccountUrl, new DefaultAzureCredential());
                });

                builder.Services.AddSingleton<IBlobContainerClient>(p =>
                {
                    var storageConfig = p.GetService<IOptions<StorageConfig>>();
                    var storageFactory = p.GetService<IStorageFactory>();
                    return storageFactory.CreateBlobContainerClient(storageConfig.Value.AppId);
                });

                // Billing service
                builder.Services.AddSingleton<IBillingClient, NullBillingClient>();

                builder.Services.AddSingleton<IJoinerFactory>(_ => new JoinerEventHubFactory());

                var atLeastOneEnabled = false;
                //Main service entry point
                if (builder.Configuration.Get<TrainerConfig>().TrainerEnabled)
                {
                    atLeastOneEnabled = true;
                    builder.Services.AddHostedService<TrainerPipeline>();
                }

                // Joiner service
                if (builder.Configuration.Get<JoinerConfig>().JoinerEnabled)
                {
                    atLeastOneEnabled = true;
                    builder.Services.AddHostedService<JoinerPipeline>();
                }

                // Log retention service
                if (builder.Configuration.Get<LogRetentionConfig>().LogRetentionEnabled)
                {
                    atLeastOneEnabled = true;
                    builder.Services.AddHostedService<LogRetentionPipeline>();
                }

                // Trainer monitoring service
                if (builder.Configuration.Get<TrainingMonitoringConfig>().TrainingMonitoringEnabled)
                {
                    atLeastOneEnabled = true;
                    builder.Services.AddHostedService<TrainingMonitoringPipeline>();
                }

                if (!atLeastOneEnabled)
                {
                    throw new ArgumentException("No services enabled. Please check your configuration.");
                }

                var host = builder.Build();
                await host.RunAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"OnlineTrainer threw exception: {e.Message}");
                throw;
            }
        }
    }
}