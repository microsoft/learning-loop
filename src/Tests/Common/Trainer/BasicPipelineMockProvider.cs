// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommonTest.Fakes.Configuration;
using CommonTest.Fakes.Messaging.EventHub;
using CommonTest.Fakes.Storage.InMemory;
using Microsoft.DecisionService.Common.Billing;
using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Trainer;
using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.DecisionService.OnlineTrainer.Operations;
using Microsoft.DecisionService.VowpalWabbit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OpenTelemetry.Trace;
using System;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using Tests.TimeProvider;

namespace Tests.Common.Trainer;

/// <summary>
/// BasicPipelineMockProvider is a helper class to create a basic pipeline
/// with mock dependencies and default configurations.
/// </summary>
internal class BasicPipelineMockProvider
{
    public const string DefaultAppId = "test-app-id";
    public const string MachineLearningTestArgsDefault_CBEVENTS = "--cb_explore_adf --epsilon 0.2 --power_t 0 -l 0.001 --cb_type ips -q ::";

    public Mock<IBillingClient> BillingClientMock = new();
    public Mock<IMeterFactory> MeterFactoryMock = new();
    public IncrementingTimeProvider TimeProvider = new(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    public Mock<TracerProvider> TracerProviderMock = new();
    public EHController EventHubController = new(4); // 4 partitions should be enough for unit testing
    public MemStorageFactory MemStorageFactory = new(MemUriHelper.CreateUri(global::CommonTest.Constants.memStoreUri));
    public MemBlobContainerClient StorageContainerClient;
    private readonly LogRetentionPiplineProvider _logRetentionProvider;
    private readonly TrainerPipelineProvider _trainerProvider;
    private readonly JoinerPipelineProvider _joinerProvider;
    private readonly TrainingMonitoringPipelineProvider _trainingMonitorProvider;

    public BasicPipelineMockProvider()
    {
        MeterFactoryMock.Setup(x => x.Create(It.IsAny<MeterOptions>())).Returns<MeterOptions>(opt => { return new Meter(opt); });
        _logRetentionProvider = new LogRetentionPiplineProvider(this);
        _trainerProvider = new TrainerPipelineProvider(this);
        _joinerProvider = new JoinerPipelineProvider(this);
        _trainingMonitorProvider = new TrainingMonitoringPipelineProvider(this);
    }

    public async Task<JoinerPipeline> CreateAsync(JoinerConfig config)
    {
        return await _joinerProvider.CreateAsync(config);
    }

    public static JoinerConfig CreateDefaultJoinerConfig()
    {
        return JoinerPipelineProvider.CreateDefaultConfig();
    }

    public async Task<JoinerPipeline> CreateJoinerWithDefaultsAsync()
    {
        return await CreateAsync(CreateDefaultJoinerConfig());
    }

    public static TrainerConfig CreateDefaultTrainerConfig()
    {
        return TrainerPipelineProvider.CreateDefaultConfig();
    }

    public async Task<TrainerPipeline> CreateAsync(TrainerConfig config)
    {
        return await _trainerProvider.CreateAsync(config);
    }

    public async Task<TrainerPipeline> CreateTrainerWithDefaultsAsync()
    {
        return await CreateAsync(CreateDefaultTrainerConfig());
    }

    public static TrainingMonitoringConfig CreateDefaultTrainingMonitoringConfig()
    {
       return TrainingMonitoringPipelineProvider.CreateDefaultConfig();
    }

    public async Task<TrainingMonitoringPipeline> CreateAsync(TrainingMonitoringConfig config)
    {
        return await _trainingMonitorProvider.CreateAsync(config);
    }

    public async Task<TrainingMonitoringPipeline> CreateTrainingMonitorWithDefaultsAsync()
    {
        return await CreateAsync(CreateDefaultTrainingMonitoringConfig());
    }

    public LogRetentionConfig CreateDefaultLogRetentionConfig()
    {
        return LogRetentionPiplineProvider.CreateDefaultConfig();
    }

    public async Task<LogRetentionPipeline> CreateAsync(LogRetentionConfig config)
    {
        return await _logRetentionProvider.CreateAsync(config);
    }

    public async Task<LogRetentionPipeline> CreateLogRetentionPipelineWithDefaultsAsync()
    {
        return await CreateAsync(CreateDefaultLogRetentionConfig());
    }

    public AnOptionsMonitor<TrainerConfig> TrainerConfigOpt => _trainerProvider.ConfigOpt;

    public AnOptionsMonitor<StorageBlockOptions> StorageBlockOptMonitor => _joinerProvider.StorageBlockOptMonitor;

    public AnOptionsMonitor<JoinerConfig> JoinerConfigOptMonitor => _joinerProvider.ConfigMonitor;

    private class TrainerPipelineProvider
    {
        private readonly BasicPipelineMockProvider _basicProvider;
        public TrainerConfig Config;
        public AnOptionsMonitor<TrainerConfig> ConfigOpt;
        public VwRunner Trainer;

        public TrainerPipelineProvider(BasicPipelineMockProvider basicProvider)
        {
            _basicProvider = basicProvider;
        }

        public static TrainerConfig CreateDefaultConfig()
        {
            return new TrainerConfig()
            {
                AppId = DefaultAppId,
                MachineLearningArguments = MachineLearningTestArgsDefault_CBEVENTS,
                LastConfigurationEditDate = new DateTime(2017, 08, 14, 0, 0, 0),
                TrainerEnabled = true,
                ModelCheckpointFrequency = TimeSpan.FromMilliseconds(10),
                ModelExportFrequency = TimeSpan.FromMilliseconds(10),
                IgnoreCheckpointAndExportFrequencyCapping = true,
                WarmstartStartDateTime = new DateTime(2017, 08, 14, 0, 0, 0),
            };
        }

        public async Task<TrainerPipeline> CreateAsync(TrainerConfig config)
        {
            Config = config;
            ConfigOpt = new AnOptionsMonitor<TrainerConfig>(Config);
            Trainer = new VwRunner(Config.MachineLearningArguments);

            _basicProvider.StorageContainerClient ??= _basicProvider.MemStorageFactory.CreateMemBlobContainerClient(Config.AppId);
            await _basicProvider.StorageContainerClient.CreateIfNotExistsAsync();

            var appIdConfig = new AppIdConfig() { AppId = this.Config.AppId };
            return new TrainerPipeline(
                this.ConfigOpt,
                this.Trainer,
                _basicProvider.MeterFactoryMock.Object,
                _basicProvider.StorageContainerClient,
                _basicProvider.TracerProviderMock.Object,
                new LoggerWithAppId<TrainerPipeline>(NullLogger<TrainerPipeline>.Instance, Options.Create(appIdConfig))
            );
        }
    }

    private class JoinerPipelineProvider
    {
        private readonly BasicPipelineMockProvider _basicProvider;
        public JoinerConfig Config;
        public AnOptionsMonitor<JoinerConfig> ConfigMonitor;
        public AnOptionsMonitor<StorageBlockOptions> StorageBlockOptMonitor;
        public IOptions<JoinerConfig> ConfigOpt;

        public JoinerPipelineProvider(BasicPipelineMockProvider basicProvider)
        {
            _basicProvider = basicProvider;
        }

        public static JoinerConfig CreateDefaultConfig()
        {
            return new JoinerConfig()
            {
                AppId = DefaultAppId,
                LastConfigurationEditDate = new DateTime(2017, 08, 14, 0, 0, 0),
                ExperimentalUnitDuration = TimeSpan.FromMilliseconds(10),
                PunctuationSlack = TimeSpan.FromMilliseconds(5),
                PunctuationTimeout = TimeSpan.FromMilliseconds(10),
                FullyQualifiedEventHubNamespace = "test-namespace",
                ActivePartitionReadTimeout = TimeSpan.FromMilliseconds(1),
                EventHubReceiveTimeout = TimeSpan.FromMilliseconds(1),
                EventReceiveTimeoutMaxRetryCount = 1,
            };
        }

        public async Task<JoinerPipeline> CreateAsync(JoinerConfig config)
        {
            Config = config;
            ConfigOpt = Options.Create(Config);
            ConfigMonitor = new AnOptionsMonitor<JoinerConfig>(Config);

            _basicProvider.StorageContainerClient ??= _basicProvider.MemStorageFactory.CreateMemBlobContainerClient(Config.AppId);
            await _basicProvider.StorageContainerClient.CreateIfNotExistsAsync();

            var storageBlockOptions = new StorageBlockOptions
            {
                AppId = Config.AppId,
                LastConfigurationEditDate = Config.LastConfigurationEditDate.GetValueOrDefault(),
                MaximumFlushLatency = new TimeSpan(0, 0, 0, 0, 10),
                RewardFunction = RewardFunction.earliest
            };

            StorageBlockOptMonitor = new AnOptionsMonitor<StorageBlockOptions>(storageBlockOptions);

            return new JoinerPipeline(
                this.ConfigMonitor,
                this.StorageBlockOptMonitor,
                _basicProvider.EventHubController.JoinerFactory,
                _basicProvider.BillingClientMock.Object,
                _basicProvider.MeterFactoryMock.Object,
                _basicProvider.TimeProvider,
                this.ConfigOpt,
                _basicProvider.TracerProviderMock.Object,
                _basicProvider.StorageContainerClient,
                NullLogger<JoinerPipeline>.Instance
            );
        }
    }

    private class TrainingMonitoringPipelineProvider
    {
        private readonly BasicPipelineMockProvider _basicProvider;
        public TrainingMonitoringConfig Config;
        public IOptions<TrainingMonitoringConfig> ConfigOpt;

        public TrainingMonitoringPipelineProvider(BasicPipelineMockProvider basicProvider)
        {
            _basicProvider = basicProvider;
        }

        public static TrainingMonitoringConfig CreateDefaultConfig()
        {
            return new TrainingMonitoringConfig()
            {
                AppId = DefaultAppId,
                TrainingMonitoringEnabled = true,
                ExperimentalUnitDuration = TimeSpan.FromMilliseconds(10),
                ModelExportFrequency = TimeSpan.FromMilliseconds(100),
                LastConfigurationEditDate = new DateTime(2017, 08, 14, 0, 0, 0),
            };
        }

        public async Task<TrainingMonitoringPipeline> CreateAsync(TrainingMonitoringConfig config)
        {
            Config = config;
            ConfigOpt = Options.Create(config);

            _basicProvider.StorageContainerClient ??= _basicProvider.MemStorageFactory.CreateMemBlobContainerClient(Config.AppId);
            await _basicProvider.StorageContainerClient.CreateIfNotExistsAsync();

            var appIdConfig = new AppIdConfig() { AppId = this.Config.AppId };
            return new TrainingMonitoringPipeline(
                ConfigOpt,
                _basicProvider.MeterFactoryMock.Object,
                _basicProvider.StorageContainerClient,
                new LoggerWithAppId<TrainingMonitoringPipeline>(NullLogger<TrainingMonitoringPipeline>.Instance, Options.Create(appIdConfig))
            );
        }
    }

    private class LogRetentionPiplineProvider
    {
        private readonly BasicPipelineMockProvider _basicProvider;
        public LogRetentionConfig Config;
        public IOptions<LogRetentionConfig> ConfigOpt;

        public LogRetentionPiplineProvider(BasicPipelineMockProvider basicProvider)
        {
            _basicProvider = basicProvider;
        }

        public static LogRetentionConfig CreateDefaultConfig()
        {
            return new LogRetentionConfig()
            {
                AppId = DefaultAppId,
                LogRetentionEnabled = true,
                LogRetentionDays = 1,
                LastConfigurationEditDate = new DateTime(2017, 08, 14, 0, 0, 0),
            };
        }

        public async Task<LogRetentionPipeline> CreateAsync(LogRetentionConfig config)
        {
            Config = config;
            ConfigOpt = Options.Create(config);

            _basicProvider.StorageContainerClient ??= _basicProvider.MemStorageFactory.CreateMemBlobContainerClient(Config.AppId);
            await _basicProvider.StorageContainerClient.CreateIfNotExistsAsync();

            return new LogRetentionPipeline(
                Options.Create(config),
                _basicProvider.StorageContainerClient,
                NullLogger<LogRetentionPipeline>.Instance
            );
        }
    }
}
