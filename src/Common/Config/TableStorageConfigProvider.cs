// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Data.Tables;
using Microsoft.DecisionService.Common.TableStorage;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;

namespace Microsoft.DecisionService.Common.Config
{
    public class TableStorageConfigSource : IConfigurationSource
    {
        private readonly TableStorageConfig _tableStorageConfig;
        private readonly string _rowKey;

        public TableStorageConfigSource(TableStorageConfig tableStorageConfig, string rowKey) =>
            (this._tableStorageConfig, this._rowKey) = (tableStorageConfig, rowKey);


        public IConfigurationProvider Build(IConfigurationBuilder builder) =>
            new TableStorageConfigProvider(this._tableStorageConfig, this._rowKey);
    }

    public sealed class TableStorageConfigProvider : ConfigurationProvider, IDisposable
    {
        private readonly TimeSpan? _reloadInterval;

        private readonly TableClient _tableClient;
        private readonly string _partitionKey;
        private readonly string _rowKey;

        private Dictionary<string, string?>? _loadedConfig;

        private Task? _pollingTask;
        private readonly CancellationTokenSource _cancellationToken;
        private bool _disposed;

        /// <summary>
        /// Creates a new <see cref="TableStorageConfigProvider"/>.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="rowKey"></param>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public TableStorageConfigProvider(
            TableStorageConfig config,
            string rowKey
        )
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            this._tableClient =
                new TableClient(config.TableStorageEndpoint, config.TableName, new DefaultAzureCredential());
            this._partitionKey = config.PartitionKey;
            this._rowKey = rowKey;

            if (config.TableStorageReloadInterval != null && config.TableStorageReloadInterval.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(config.TableStorageReloadInterval),
                    config.TableStorageReloadInterval,
                    nameof(config.TableStorageReloadInterval) + " must be positive.");
            }

            _pollingTask = null;
            _cancellationToken = new CancellationTokenSource();
            _reloadInterval = config.TableStorageReloadInterval;
        }

        private async Task PollForSecretChangesAsync()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                await WaitForReloadAsync().ConfigureAwait(false);
                try
                {
                    await LoadAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Ignore
                }
            }
        }

        private Task WaitForReloadAsync()
        {
            // WaitForReload is only called when the _reloadInterval has a value.
            return Task.Delay(_reloadInterval.Value, _cancellationToken.Token);
        }

        public override void Load() => LoadAsync().GetAwaiter().GetResult();

        private static object ConvertObjectFromAzureTable(object obj)
        {
            switch (obj)
            {
                case DateTime time:
                {
                    DateTime dateTime = time.ToUniversalTime();
                    return dateTime <= TableManagerConstants.MinDateTime
                        ? DateTime.MinValue.ToUniversalTime()
                        : dateTime;
                }
                case DateTimeOffset offset:
                {
                    DateTime dateTime = offset.ToUniversalTime().DateTime;
                    return dateTime < TableManagerConstants.MinDateTime
                        ? DateTime.MinValue.ToUniversalTime()
                        : dateTime;
                }
                default:
                    return obj;
            }
        }

        private async Task LoadAsync()
        {
            var newLoadedConfig = new Dictionary<string, string?>();
            var oldLoadedConfig = Interlocked.Exchange(ref _loadedConfig, null);
            var retrieveTask = this._tableClient.GetEntityAsync<TableEntity>(this._partitionKey, this._rowKey);
            TableEntity entity = await retrieveTask;
            int newlyDiscoveredItems = 0;
            foreach (var kv in entity)
            {
                if (kv.Key == "odata.etag" || kv.Key == "Timestamp")
                {
                    continue;
                }

                var key = kv.Key.Replace(Constants.TABLEPROPERTY_SECTION_DELIMITER, Constants.CONFIG_DELIMITER);
                var value = ConvertObjectFromAzureTable(kv.Value).ToString();
                if (oldLoadedConfig != null && oldLoadedConfig.TryGetValue(key, out var existingConfigItem) &&
                    existingConfigItem == value)
                {
                    oldLoadedConfig.Remove(key);
                    newLoadedConfig.Add(key, value);
                }
                else
                {
                    newlyDiscoveredItems++;
                    newLoadedConfig.Add(key, value);
                }
            }

            _loadedConfig = newLoadedConfig;

            if (newlyDiscoveredItems > 0 || oldLoadedConfig?.Any() == true)
            {
                Data = newLoadedConfig;
                if (oldLoadedConfig != null)
                {
                    OnReload();
                }
            }

            if (_pollingTask == null && _reloadInterval != null)
            {
                _pollingTask = PollForSecretChangesAsync();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!_disposed)
                {
                    _cancellationToken.Cancel();
                    _cancellationToken.Dispose();
                }

                _disposed = true;
            }
        }
    }
}