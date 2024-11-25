// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Trainer;
using Microsoft.DecisionService.Instrumentation;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Microsoft.DecisionService.OnlineTrainer
{
    public sealed class JoinerReceiverMetrics
    {
        private readonly KeyValuePair<string, object?> _appIdProperty;

        private readonly UpDownCounter<int> _numEventHubProcessors;
        private readonly Counter<long> _joinerReceiverEventsReceived;
        
        private readonly Counter<long> _reconnectException;
        private readonly Histogram<long> _joinerReceiverEventSize;

        public JoinerReceiverMetrics(IOptions<AppIdConfig> appId, IMeterFactory meterFactory) : this(appId.Value.AppId, meterFactory)
        {
        }


        public JoinerReceiverMetrics(string appId, IMeterFactory meterFactory)
        {
            _appIdProperty = new KeyValuePair<string, object?>(MetricsUtil.AppIdKey, appId);

            var meter = meterFactory?.Create("Microsoft.DecisionService.OnlineTrainer.JoinerReceiverMetrics", "1.0");
            _numEventHubProcessors = meter?.CreateUpDownCounter<int>("EventHubProcessorCount");
            _joinerReceiverEventsReceived = meter?.CreateCounter<long>("JoinerReceiverEventsReceived");
            _reconnectException = meter?.CreateCounter<long>("ReconnectException");
            _joinerReceiverEventSize = meter?.CreateHistogram<long>("JoinerReceiverEventSize");
        }

        public void EventHubProcessorsIncrement()
        {
            _numEventHubProcessors?.Add(1, _appIdProperty);
        }

        public void EventHubProcessorsDecrement()
        {
            _numEventHubProcessors?.Add(-1, _appIdProperty);
        }

   
        public void JoinerReceivedEventsReceived(long count, string streamName)
        {
            _joinerReceiverEventsReceived?.Add(count, new []
            {
                new KeyValuePair<string, object?>("StreamName", streamName),
                _appIdProperty
            });
        }
        
        public void JoinerReceivedMessageReceivedSize(long size, string streamName)
        {
            _joinerReceiverEventSize?.Record(size, new []
            {
                new KeyValuePair<string, object?>("StreamName", streamName),
                _appIdProperty
            });
        }

        public void ReconnectExceptionIncrement()
        {
            _reconnectException?.Add(1, _appIdProperty);
        }
    }
}