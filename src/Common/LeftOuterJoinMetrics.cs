// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Trainer;
using Microsoft.DecisionService.Instrumentation;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Microsoft.DecisionService.OnlineTrainer
{
    public sealed class LeftOuterJoinMetrics
    {
        private readonly KeyValuePair<string, object?> _appIdProperty;

        /// Tracks when an interaction is received by the LOJ block.
        private readonly Counter<long> _interactionEventReceived;

        /// Tracks when an observation is received by the LOJ block.
        private readonly Counter<long> _observationEventReceived;

        private readonly Counter<long> _nullInteractionEventReceived;
        private readonly Counter<long> _nullObservationEventReceived;

        /// Tracks when observations are removed from dangling observations list
        /// and are no longer eligible to be joined with an interaction.
        private readonly Counter<long> _observationEvicted;

        /// Tracks when an interaction is processed that has an enqueued time earlier
        /// than the last processed interaction.
        private readonly Counter<long> _outOfOrderInteractionEventReceived;

        /// Tracks when an observation is processed that has an enqueued time earlier
        /// than the last processed observation.
        private readonly Counter<long> _outOfOrderObservationEventReceived;

        /// Tracks when an interaction is joined with a dangling observation
        private readonly Counter<long> _joinedInteractionWithDanglingObservation;

        /// Tracks when a matching observation was found, but it was not in
        /// the interaction ready time window.
        private readonly Counter<long> _joinedObservationNotInInteractionReadyWindow;

        /// Tracks when the currently processed interaction is joined with an incoming observation.
        private readonly Counter<long> _joinedObservationWithCurrentInteraction;

        /// Tracks when an interaction is joined with an observation.
        private readonly Counter<long> _joinedInteractionWithObservation;

        /// Tracks when an interaction is processed and not matching
        /// observation is found.
        private readonly Counter<long> _noMatchingObservationForInteraction;

        /// <summary>
        /// Tracks when receiving interactions times out.
        /// </summary>
        /// <remarks>
        /// This is noteworthy because we adjust timestamps that are
        /// used when joining events when this happens.
        /// </remarks>
        private readonly Counter<long> _interactionReceiveTimeout;

        /// Tracks when an observation is received by the LOJ block.
        private readonly Counter<long> _observationReceiveTimeout;

        /// <summary>
        /// Tracks when we give up trying to get observations for the current interaction
        /// because receive timed out and too much time has passed.
        /// </summary>
        private readonly Counter<long> _stoppedTryingToReceiveObservationsForInteraction;

        /// Tracks the time between when an interaction is enqueued in event hub
        /// and when the matching observation (reward or activation) is enqueued in event hub.
        private readonly Histogram<double> _observationLatency;

        /// Tracks the time between when an interaction is enqueued in event hub
        /// and when the matching activation is enqueued in event hub.
        private readonly Histogram<double> _activationLatency;

        /// Tracks the time between when an interaction is enqueued in event hub
        /// and when the matching reward is enqueued in event hub.
        private readonly Histogram<double> _rewardLatency;

        /// Pdrop is the probability that the client drops the event.
        private readonly Histogram<double> _pdropValue;

        /// <summary>
        /// Tracks if the joiner is keeping up with the rank calls.
        /// The time it takes to join should the same as EUD.
        /// Delay = Current Time - Interaction EH enqueue time - EUD.
        /// </summary>
        private readonly Histogram<double> _joinerDelayFromEnqueuedTime;

        private readonly ObservableGauge<long> _danglingObservations;
        private long _danglingObservationsCount;
        private readonly ObservableGauge<long> _danglingActvations;
        private long _danglingActvationsCount;


        // TODO segment into "verbose" metrics
        public LeftOuterJoinMetrics(IOptions<AppIdConfig> appId, IMeterFactory meterFactory, bool verboseMetrics) : this(
            appId.Value.AppId, meterFactory, verboseMetrics)
        {
        }

        public LeftOuterJoinMetrics(string appId, IMeterFactory meterFactory, bool verboseMetrics)
        {
            _appIdProperty = new KeyValuePair<string, object?>(MetricsUtil.AppIdKey, appId);

            var meter = meterFactory?.Create("Microsoft.DecisionService.OnlineTrainer.LeftOuterJoinMetrics", "1.0");
            if (verboseMetrics)
            {
                _interactionEventReceived = meter?.CreateCounter<long>("InteractionEventReceived");
                _observationEventReceived = meter?.CreateCounter<long>("ObservationEventReceived");
                _nullInteractionEventReceived = meter?.CreateCounter<long>("NullInteractionEventReceived");
                _nullObservationEventReceived = meter?.CreateCounter<long>("NullObservationEventReceived");
                _outOfOrderInteractionEventReceived = meter?.CreateCounter<long>("OutOfOrderInteractionEventReceived");
                _outOfOrderObservationEventReceived = meter?.CreateCounter<long>("OutOfOrderObservationEventReceived");
                _joinedInteractionWithDanglingObservation =
                    meter?.CreateCounter<long>("JoinedInteractionWithDanglingObservation");
                _joinedObservationNotInInteractionReadyWindow =
                    meter?.CreateCounter<long>("JoinedObservationNotInInteractionReadyWindow");
                _joinedObservationWithCurrentInteraction =
                    meter?.CreateCounter<long>("JoinedObservationWithCurrentInteraction");
                _noMatchingObservationForInteraction =
                    meter?.CreateCounter<long>("NoMatchingObservationForInteraction");
                _interactionReceiveTimeout = meter?.CreateCounter<long>("InteractionReceiveTimeout");
                _observationReceiveTimeout = meter?.CreateCounter<long>("ObservationReceiveTimeout");
                _stoppedTryingToReceiveObservationsForInteraction =
                    meter?.CreateCounter<long>("StoppedTryingToReceiveObservationsForInteraction");
            }

            _observationLatency = meter?.CreateHistogram<double>("ObservationLatency");
            _activationLatency = meter?.CreateHistogram<double>("ActivationLatency");
            _rewardLatency = meter?.CreateHistogram<double>("RewardLatency");
            _pdropValue = meter?.CreateHistogram<double>("PdropValue");
            _joinerDelayFromEnqueuedTime = meter?.CreateHistogram<double>("JoinerDelayFromEnqueuedTime");

            _observationEvicted = meter?.CreateCounter<long>("ObservationEvicted");
            _joinedInteractionWithObservation = meter?.CreateCounter<long>("JoinedInteractionWithObservation");

            IEnumerable<KeyValuePair<string, object>> tags = new[] { _appIdProperty };
            _danglingObservations = meter?.CreateObservableGauge<long>("DanglingObservations",
                () => _danglingObservationsCount, null, null, tags);
            _danglingActvations = meter?.CreateObservableGauge<long>("DanglingActvations",
                () => _danglingActvationsCount, null, null, tags);
        }

        public void InteractionEventReceived()
        {
            _interactionEventReceived?.Add(1, _appIdProperty);
        }

        public void ObservationEventReceived()
        {
            _observationEventReceived?.Add(1, _appIdProperty);
        }

        public void NullInteractionEventReceived()
        {
            _nullInteractionEventReceived?.Add(1, _appIdProperty);
        }

        public void NullObservationEventReceived()
        {
            _nullObservationEventReceived?.Add(1, _appIdProperty);
        }

        public void ObservationsEvicted(long count)
        {
            _observationEvicted?.Add(count, _appIdProperty);
        }

        public void OutOfOrderInteractionEventReceived()
        {
            _outOfOrderInteractionEventReceived?.Add(1, _appIdProperty);
        }

        public void OutOfOrderObservationEventReceived()
        {
            _outOfOrderObservationEventReceived?.Add(1, _appIdProperty);
        }

        public void JoinedInteractionWithDanglingObservation()
        {
            _joinedInteractionWithDanglingObservation?.Add(1, _appIdProperty);
        }

        public void JoinedObservationNotInInteractionReadyWindow()
        {
            _joinedObservationNotInInteractionReadyWindow?.Add(1, _appIdProperty);
        }

        public void JoinedObservationWithCurrentInteraction()
        {
            _joinedObservationWithCurrentInteraction?.Add(1, _appIdProperty);
        }

        public void JoinedInteractionWithObservation()
        {
            _joinedInteractionWithObservation?.Add(1, _appIdProperty);
        }

        public void NoMatchingObservationForInteraction()
        {
            _noMatchingObservationForInteraction?.Add(1, _appIdProperty);
        }

        public void InteractionReceiveTimeout()
        {
            _interactionReceiveTimeout?.Add(1, _appIdProperty);
        }

        public void ObservationReceiveTimeout()
        {
            _observationReceiveTimeout?.Add(1, _appIdProperty);
        }

        public void StoppedTryingToReceiveObservationsForInteraction()
        {
            _stoppedTryingToReceiveObservationsForInteraction?.Add(1, _appIdProperty);
        }

        public void ObservationLatency(double latency)
        {
            _observationLatency?.Record(latency, _appIdProperty);
        }

        public void ActivationLatency(double latency)
        {
            _activationLatency?.Record(latency, _appIdProperty);
        }

        public void RewardLatency(double latency)
        {
            _rewardLatency?.Record(latency, _appIdProperty);
        }

        public void PdropValue(double pdrop)
        {
            _pdropValue?.Record(pdrop, _appIdProperty);
        }

        public void JoinerDelayFromEnqueuedTime(double delay)
        {
            _joinerDelayFromEnqueuedTime?.Record(delay, _appIdProperty);
        }

        public void DanglingObservations(long count)
        {
            _danglingObservationsCount = count;
        }

        public void DanglingActvations(long count)
        {
            _danglingActvationsCount = count;
        }
    }
}