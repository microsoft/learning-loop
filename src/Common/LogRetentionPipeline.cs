// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.DecisionService.Common.Storage;

namespace Microsoft.DecisionService.Common.Trainer
{
    public class LogRetentionConfig
    {
        [Required]
        public string AppId { get; set; }
        public bool LogRetentionEnabled { get; set; } = false;
        public double LogRetentionDays { get; set; }

        [Required]
        public DateTime LastConfigurationEditDate { get; set; }
    }

    public sealed class LogRetentionPipeline : IDisposable, IHostedService
    {
        private readonly LogRetentionConfig _logRetentionConfig;
        private readonly string _appId;
        private readonly ILogger _logger;
        private readonly IBlobContainerClient _blobContainerClient;
        private readonly CancellationTokenSource _tokenSource;

        private Task _completion;
        private IBlobLeaseHolder _blobLease;

        public LogRetentionPipeline(
            IOptions<LogRetentionConfig> logRetentionConfig,
            IBlobContainerClient blobContainerClient,
            ILogger<LogRetentionPipeline> logger)
        {
            this._logRetentionConfig = logRetentionConfig.Value;
            this._appId = logRetentionConfig.Value.AppId;
            this._logger = logger;
            this._blobContainerClient = blobContainerClient;
            this._tokenSource = new CancellationTokenSource();
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger?.LogInformation(
                    message:
                    $"Log retention pipeline started with LogRetentionDays:{_logRetentionConfig.LogRetentionDays}");

                await RunLogRetentionTaskAsync(this._tokenSource.Token);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var utcNow = DateTime.UtcNow;

                    DateTime scheduledForUtc = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, 1);
                    if (_logRetentionConfig.LogRetentionDays <= 1)
                    {
                        scheduledForUtc = scheduledForUtc.AddHours(utcNow.Hour).AddHours(1);
                    }
                    else
                    {
                        scheduledForUtc = scheduledForUtc.AddDays(1);
                    }

                    TimeSpan cleanupDelay = scheduledForUtc - utcNow;

                    await Task.Delay(cleanupDelay, cancellationToken)
                        .ContinueWith(
                            async (x) => await RunLogRetentionTaskAsync(cancellationToken),
                            cancellationToken,
                            TaskContinuationOptions.None,
                            TaskScheduler.Default);
                }
            }
            catch (TaskCanceledException)
            {
                _logger?.LogInformation("TaskCanceledException Exiting LogRetentionPipeline RunAsync()");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "LogRetentionPipeline.RunAsync");
            }
            finally
            {
                _logger?.LogInformation("finally Exiting LogRetentionPipeline RunAsync()");
            }
        }

        public void Dispose()
        {
            this._tokenSource.Cancel();
            this._blobLease.Dispose();
        }

        private async Task RunLogRetentionTaskAsync(CancellationToken cancellationToken)
        {
            try
            {
                DateTime now = DateTime.UtcNow;
                _logger?.LogInformation(
                    message:
                    $"Executing log retention task LogRetentionDays:{_logRetentionConfig.LogRetentionDays} at {now} Utc");
                IList<string> deletedBlobsUri = await PersonalizationStorageHelper.DeleteExpiredBlobsAsync(
                    _blobContainerClient, _appId, _logRetentionConfig.LogRetentionDays, now, cancellationToken);
                if (deletedBlobsUri != null)
                {
                    LogDeletedBlobsMessage(deletedBlobsUri, _logger);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "LogRetentionPipeline.RunLogRetentionTaskAsync");
            }
        }

        private void LogDeletedBlobsMessage(IList<string> deletedBlobsUri, ILogger appIdLogger)
        {
            foreach (string blobUri in deletedBlobsUri)
            {
                _logger?.LogInformation(message: $"Deleting blob {blobUri}");
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // acquire lease before being able to start.
            // TODO link the cancellationToken passed in here to the internal token source to disentangle initial acquisition from ongoing renewal
            _blobLease = await _blobContainerClient.AcquireLeaseAsync(_appId, "LogRetentionLock",
                _logRetentionConfig.LastConfigurationEditDate, _logger, cancellationToken);
            _completion = Task.Run(() => this.RunAsync(_tokenSource.Token));
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _tokenSource.Cancel();
            await Task.WhenAny(Task.WhenAll(_blobLease.Completion, _completion), Task.Delay(-1, cancellationToken));
        }
    }
}