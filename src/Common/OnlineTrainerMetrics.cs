// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Instrumentation;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Microsoft.DecisionService.Common.Trainer;
using Microsoft.Extensions.Options;

namespace Microsoft.DecisionService.OnlineTrainer
{
    public sealed class OnlineTrainerMetrics
    {
        private readonly KeyValuePair<string, object?> _appIdProperty;

        private readonly Counter<long> _learnedEvents;
        private readonly Counter<long> _faultyExamplesTotal;

        private readonly Counter<long> _learnException;
        private readonly Counter<long> _modelCheckpointed;
        private readonly Counter<long> _modelExported;

        private readonly Histogram<double> _featuresPerExample;
        private readonly Histogram<double> _featuresPerEvent;
        private readonly Histogram<double> _actionsPerEvent;
        private readonly Histogram<double> _namespacesPerExample;
        private readonly Histogram<double> _namespacesPerEvent;

        private readonly Histogram<double> _learnTimeMs;
        private readonly Histogram<long> _cookedLogSizeInBytes;

        private ObservableGauge<double> _learnThroughputInEventsPerMS;
        private double _learnThroughputInEventsPerMSValue;

        private ObservableGauge<double> _learnThroughputInBytesPerMS;
        private double _learnThroughputInBytesPerMSValue;


        // TODO segment into "verbose" metrics
        public OnlineTrainerMetrics(IOptions<AppIdConfig> appId, IMeterFactory meterFactory) : this(
            appId.Value.AppId, meterFactory)
        {
        }


        public OnlineTrainerMetrics(string appId, IMeterFactory meterFactory)
        {
            _appIdProperty = new KeyValuePair<string, object?>(MetricsUtil.AppIdKey, appId);

            var meter = meterFactory?.Create("Microsoft.DecisionService.OnlineTrainer.OnlineTrainerMetrics", "1.0");


            _learnedEvents = meter?.CreateCounter<long>("Trainer.LearnTotal");
            _faultyExamplesTotal = meter?.CreateCounter<long>("Trainer.FaultyExamplesTotal");

            _learnException = meter?.CreateCounter<long>("Trainer.LearnException");
            _modelExported = meter?.CreateCounter<long>("Trainer.ModelExported");
            _modelCheckpointed = meter?.CreateCounter<long>("Trainer.ModelCheckpointed");
            _modelExported = meter?.CreateCounter<long>("Trainer.ModelExported");

            _featuresPerExample = meter?.CreateHistogram<double>("Trainer.FeaturesPerExample");
            _featuresPerEvent = meter?.CreateHistogram<double>("Trainer.FeaturesPerEvent");
            _actionsPerEvent = meter?.CreateHistogram<double>("Trainer.ActionsPerEvent");
            _namespacesPerExample = meter?.CreateHistogram<double>("Trainer.NamespacesPerExample");
            _namespacesPerEvent = meter?.CreateHistogram<double>("Trainer.NamespacesPerEvent");

            _learnTimeMs = meter?.CreateHistogram<double>("Trainer.LearnTimeMs");
            _cookedLogSizeInBytes = meter?.CreateHistogram<long>("Trainer.CookedLogSizeInBytes");

            _learnThroughputInEventsPerMS = meter?.CreateObservableGauge<double>("Trainer.LearnThroughputInEventsPerMS",
                () => _learnThroughputInEventsPerMSValue, null, null, new[] { _appIdProperty });

            _learnThroughputInBytesPerMS = meter?.CreateObservableGauge<double>("Trainer.LearnThroughputInBytesPerMS",
                () => _learnThroughputInBytesPerMSValue, null, null, new[] { _appIdProperty });
        }

        public void LearnedEvents(long count)
        {
            _learnedEvents?.Add(count, _appIdProperty);
        }

        public void FaultyExamples(long count)
        {
            _faultyExamplesTotal?.Add(count, _appIdProperty);
        }

        public void LearnException()
        {
            _learnException?.Add(1, _appIdProperty);
        }

        public void ModelCheckpointed()
        {
            _modelCheckpointed?.Add(1, _appIdProperty);
        }

        public void ModelExported()
        {
            _modelExported?.Add(1, _appIdProperty);
        }

        public void FeaturesPerExample(double value)
        {
            _featuresPerExample?.Record(value, _appIdProperty);
        }

        public void FeaturesPerEvent(double value)
        {
            _featuresPerEvent?.Record(value, _appIdProperty);
        }

        public void ActionsPerEvent(double value)
        {
            _actionsPerEvent?.Record(value, _appIdProperty);
        }

        public void NamespacesPerExample(double value)
        {
            _namespacesPerExample?.Record(value, _appIdProperty);
        }

        public void NamespacesPerEvent(double value)
        {
            _namespacesPerEvent?.Record(value, _appIdProperty);
        }

        public void LearnTimeMs(double value)
        {
            _learnTimeMs?.Record(value, _appIdProperty);
        }

        public void CookedLogSizeInBytes(long value)
        {
            _cookedLogSizeInBytes?.Record(value, _appIdProperty);
        }

        public void LearnThroughputInEventsPerMS(double value)
        {
            _learnThroughputInEventsPerMSValue = value;
        }

        public void LearnThroughputInBytesPerMS(double value)
        {
            _learnThroughputInBytesPerMSValue = value;
        }
    }
}