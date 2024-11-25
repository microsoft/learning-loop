// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Error;
using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Microsoft.DecisionService.Common.Join
{
    public enum InvalidMessageReason
    {
        EvictedObservation,
    }

    public class InvalidMessage
    {
        public Message Message;
        public InvalidMessageReason Reason;
    }

    public sealed class LeftOuterJoinBlock
    {
        // Interactions
        private readonly ISourceBlock<MessageBatch> _left;

        // Observations
        private readonly ISourceBlock<Message> _right;

        private readonly ITargetBlock<JoinedBatch> _output;

        private readonly ITargetBlock<InvalidMessage>? _invalidMessages = null;

        private readonly JoinerConfig _config;

        private readonly Dictionary<string, List<Message>> _danglingObservationMap;
        private int evictedDanglingObservations = 0;

        private readonly TimeSpan _experimentalUnitDuration;

        private readonly TimeSpan _backwardEventJoinWindowTimeSpan;

        private readonly TimeSpan _punctuationTimeout;

        private readonly TimeSpan _punctuationSlack;

        private readonly ILogger _logger;

        private readonly LeftOuterJoinMetrics _metrics;

        private readonly ITimeProvider _timeProvider;

        public LeftOuterJoinBlock(ISourceBlock<MessageBatch> left, ISourceBlock<Message> right,
            ITargetBlock<JoinedBatch> joinedTarget, JoinerConfig config, ITimeProvider timeProvider,
            IMeterFactory meterFactory, ILogger logger, CancellationToken cancellationToken)
        {
            this._left = left;
            this._right = right;
            this._output = joinedTarget;
            this._config = config ?? throw new ArgumentNullException(nameof(config));
            _metrics = new LeftOuterJoinMetrics(config.AppId, meterFactory,
                config.LOJVerboseMetricsEnabled);

            _timeProvider = timeProvider;


            _logger = logger;

            this._danglingObservationMap = new Dictionary<string, List<Message>>();

            this._experimentalUnitDuration = this._config.ExperimentalUnitDuration ?? ApplicationConstants.DefaultExperimentalUnitDuration;
            this._backwardEventJoinWindowTimeSpan = this._config.BackwardEventJoinWindowTimeSpan;
            this._punctuationTimeout = this._config.PunctuationTimeout;
            this._punctuationSlack = this._config.PunctuationSlack;

            // Do not wait for left or right block to be completed here.
            // In order for left or right block to be completed, both blocks should be either faulty/cancel state,
            // Or all the events in the blocks should be drained. Eventhub checkpoint block is updated after the data
            // is updated. So it's fine to abandon the data in the memory.
            this.Completion = this.RunAsync(cancellationToken);
            this.Completion.TraceAsync(_logger, "LOJ", "LOJ.OnExit");
        }

        public int DanglingObservationsCount
        {
            get
            {
                int count = 0;
                foreach (var danglingObservationList in this._danglingObservationMap.Values)
                {
                    count += danglingObservationList.Count;
                }

                return count + evictedDanglingObservations;
            }
        }


        // keep MinValue.Otherwise the first interaction will be considered out of order for historical events.
        public DateTime LastInteractionProcessedTimestamp { get; private set; } = DateTime.MinValue;

        public DateTime InteractionReadyTimestamp { get; private set; }

        // keep MinValue. Otherwise the first observation will be considered out of order for historical events.
        public DateTime LastObservationTimestamp { get; private set; } = DateTime.MinValue;

        public Task Completion { get; private set; }

        private bool IsMatchObservationInteraction(DateTime? interactionEnqueuedTimeUtc,
            DateTime? observationEnqueuedTimeUtc)
        {
            if (interactionEnqueuedTimeUtc == null || observationEnqueuedTimeUtc == null)
            {
                return false;
            }

            if (observationEnqueuedTimeUtc - interactionEnqueuedTimeUtc >= this._experimentalUnitDuration)
            {
                return false;
            }

            if (interactionEnqueuedTimeUtc - observationEnqueuedTimeUtc >= this._backwardEventJoinWindowTimeSpan)
            {
                return false;
            }

            return true;
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                // need to keep the received observation task between interactions
                // consider the following scenario:
                // 1. interaction #1 is pulled
                // 2. we ReceiveAsync an observation (w/o) fully awaiting
                // 3. timeout for observation kicks in as it didn't come in time
                // 4. interaction #2 is pulled
                // 5. since we have an open ReceiveAsync/observation task, we need to try to await the same again
                int intEventReceiveTimeoutRetryCount = 0;
                while (!cancellationToken.IsCancellationRequested)
                {
                    MessageBatch interaction = null;

                    try
                    {
                        interaction =
                            await this._left.ReceiveAsync(this._punctuationTimeout, cancellationToken);
                        // reset timeout retry counter on successful receive.
                        intEventReceiveTimeoutRetryCount = 0;
                        if (interaction == null)
                        {
                            this._metrics.NullInteractionEventReceived();
                        }
                        else
                        {
                            // CCB or slates interactions will have sub-events,
                            // but for this purpose we only count as one event.
                            this._metrics.InteractionEventReceived();
                        }
                    }
                    catch (TimeoutException)
                    {
                        // Do nothing but report the failure and retry
                        _metrics.InteractionReceiveTimeout();
                        if (intEventReceiveTimeoutRetryCount < this._config.EventReceiveTimeoutMaxRetryCount)
                        {
                            intEventReceiveTimeoutRetryCount++;
                            continue;
                        }
                    }
                    catch (InvalidOperationException e)
                    {
                        this._logger?.LogError(e, "LOJ.RunAsync.Exit");
                        throw;
                    }

                    if (interaction == null)
                    {
                        // This is a trade-off solution. Still use 1st to keep logic mostly same as previous implementation for production stability.
                        // 1. this.LastInteractionProcessedTimestamp = DateTime.UtcNow - this.punctuationSlack; is coming from punctuation event solution.
                        // It has the bug if timeout happened in the middle of reading historical data, some logs between last enqueue time to UtcNow -
                        // punctuationSlack could not be joined.
                        // 2. this.LastInteractionProcessedTimestamp = this.LastInteractionProcessedTimestamp.Add(this.options.PunctuationTimeout);
                        // this solution won't miss any joint data. However, if the historical data was pretty old, e.g. a few days. The timer will only grow every
                        // 2 seconds in each iteration. Causing slow to join. But this should be resolved once connection to eventhub is normal.
                        this.LastInteractionProcessedTimestamp =
                            AdjustByPunctuationSlack(this.LastInteractionProcessedTimestamp);
                        await this.EvictObservationsAsync(cancellationToken);
                        continue;
                    }

                    DateTime currentInteractionTimestamp = interaction.EnqueuedTimeUtc;

                    // Adding log for BUG 1433: to tell if late arrived logs are coming from eventhub.
                    if (this.LastInteractionProcessedTimestamp.CompareTo(currentInteractionTimestamp) > 0)
                    {
                        _metrics.OutOfOrderInteractionEventReceived();
                        _logger.LogWarning("LOJ Interaction not arriving in order:  " +
                                           "Last Interaction time {LastInteractionProcessedTimestamp}  " +
                                           "Current Interaction time {EnqueuedTimeUtc}  " +
                                           "Current Interaction Id {EventId}",
                            this.LastInteractionProcessedTimestamp,
                            interaction.EnqueuedTimeUtc,
                            ""
                        );
                    }

                    // need to move this time forward to make sure this.EvictObservations() is able to clean
                    this.LastInteractionProcessedTimestamp = currentInteractionTimestamp;

                    JoinedBatch? joinedEvents = new JoinedBatch()
                    {
                        EnqueuedTimeUtc = interaction.EnqueuedTimeUtc,
                        Offset = interaction.Offset,
                        PartitionId = interaction.PartitionId,
                        SequenceNumber = interaction.SequenceNumber,
                        Messages = new List<List<Message>>()
                    };
                    foreach (var currentInteraction in interaction.Messages)
                    {
                        bool joinedInteraction = false;

                        // Non joinable events don't have a valid event-id to be joined and should be ignored.
                        if (!currentInteraction.IsJoinableEvent)
                        {
                            continue;
                        }

                        List<Message> joinedEvent = new List<Message>();
                        joinedEvent.Add(currentInteraction);

                        // Step 1. Find matching observations in dangling observations.
                        bool foundDanglingObsList =
                            this._danglingObservationMap.TryGetValue(currentInteraction.EventId,
                                out var danglingObservationList);
                        if (foundDanglingObsList)
                        {
                            var (valid, evicted) = danglingObservationList.PartitionByPredicate(obs =>
                                this.IsMatchObservationInteraction(currentInteraction.EnqueuedTimeUtc,
                                    obs.EnqueuedTimeUtc));
                            joinedEvent.AddRange(valid);
                            this._danglingObservationMap.Remove(currentInteraction.EventId);

                            foreach (var observation in valid)
                            {
                                var obsLatency = (observation.EnqueuedTimeUtc -
                                                  currentInteraction.EnqueuedTimeUtc)
                                    .TotalMilliseconds;
                                _metrics.ObservationLatency(obsLatency);
                            }

                            foreach (var observation in evicted)
                            {
                                _metrics.JoinedObservationNotInInteractionReadyWindow();
                                _metrics.ObservationsEvicted(1);
                                evictedDanglingObservations++;
                                // TODO, send all and await resulting list
                                if (_invalidMessages != null)
                                {
                                    await _invalidMessages?.SendAsync(new InvalidMessage()
                                    {
                                        Message = observation,
                                        Reason = InvalidMessageReason.EvictedObservation
                                    }, cancellationToken);
                                }
                            }
                        }

                        // Step 2. Find matching observations in newly received observations.

                        this.InteractionReadyTimestamp =
                            currentInteraction.EnqueuedTimeUtc.Add(this._experimentalUnitDuration);
                        if (this.InteractionReadyTimestamp > this.LastObservationTimestamp)
                        {
                            // reverse is easier
                            // if interaction ready timestamp is before lastObservationTimestamp we can skip and output event

                            // this variable is to check the progress we have listened from interaction.EnqueuedTimeUtc to InteractionReadyTimestamp.
                            DateTime currentRewardExpectDateTime = interaction.EnqueuedTimeUtc;
                            int obsEventReceiveTimeoutRetryCount = 0;
                            do
                            {
                                // Keep trying to get observations until we give up because our expected reward time
                                // exceeds the experimental unit duration.
                                Message? observation = null;
                                do
                                {
                                    try
                                    {
                                        observation = await this._right.ReceiveAsync(this._punctuationTimeout,
                                            cancellationToken);
                                        // reset timeout retry counter on successful receive.
                                        obsEventReceiveTimeoutRetryCount = 0;
                                        if (observation == null)
                                        {
                                            _metrics.NullObservationEventReceived();
                                        }
                                        else
                                        {
                                            _metrics.ObservationEventReceived();
                                        }
                                    }
                                    catch (TimeoutException)
                                    {
                                        observation = null;
                                        _metrics.ObservationReceiveTimeout();
                                        if (obsEventReceiveTimeoutRetryCount <
                                            this._config.EventReceiveTimeoutMaxRetryCount)
                                        {
                                            obsEventReceiveTimeoutRetryCount++;
                                            continue;
                                        }

                                        // This is a trade-off solution. Still use 1st to keep logic mostly same as previous implementation for production stability.
                                        // 1. currentRewardExpectDateTime = DateTime.UtcNow - this.punctuationSlack; is coming from punctuation event solution.
                                        // It has the bug if timeout happens in the middle of reading historical data, some logs between last enqueutime to UtcNow -
                                        // punctionSlack could not be joined.
                                        // 2. currentRewardExpectDateTime = currentRewardExpectDateTime.Add(this.options.PunctuationTimeout);
                                        // this solution won't miss any joined data. However, if the historical data was pretty old, e.g. a few days. The timer will only grow every
                                        // punctuation timeout seconds in each iteration. Causing slow to join. But this should be resolved once connection to eventhub is normal.
                                        currentRewardExpectDateTime =
                                            AdjustByPunctuationSlack(currentRewardExpectDateTime);
                                        if (currentRewardExpectDateTime.CompareTo(this.InteractionReadyTimestamp) >= 0)
                                        {
                                            _metrics.StoppedTryingToReceiveObservationsForInteraction();
                                            break;
                                        }
                                    }
                                }
                                // handle non-parse-able events
                                while (observation == null);

                                // If we have passed experimental duration, do not wait for more rewards.
                                if (observation == null)
                                {
                                    this.LastObservationTimestamp =
                                        AdjustByPunctuationSlack(this.LastObservationTimestamp);
                                    break;
                                }

                                DateTime currentObservationTimestamp = observation.EnqueuedTimeUtc;
                                currentRewardExpectDateTime = observation.EnqueuedTimeUtc;

                                // Adding log for BUG 1433: to tell if late arrived logs are coming from eventhub.
                                if (this.LastObservationTimestamp.CompareTo(currentObservationTimestamp) > 0)
                                {
                                    _metrics.OutOfOrderObservationEventReceived();
                                    this._logger?.LogInformation("LOJ Observation not arriving in order: " +
                                                                 "Last Observation time {LastObservationTimestamp} " +
                                                                 "Current Observation time {EnqueuedTimestamp} " +
                                                                 "Current Observation Id {EventId} LOJ.Observation"
                                        ,
                                        this.LastObservationTimestamp,
                                        observation.EnqueuedTimeUtc,
                                        observation.EventId
                                    );
                                }

                                this.LastObservationTimestamp = currentObservationTimestamp;

                                this._logger?.LogDataFlowTrace(observation.EventId, "ObservationEvent",
                                    "LOJ.Observation", observation.EnqueuedTimeUtc);

                                // does this observation match the current event, and are we still in the matching window?
                                if (observation.EventId == currentInteraction.EventId)
                                {
                                    var obsLatency = (observation.EnqueuedTimeUtc -
                                                      currentInteraction.EnqueuedTimeUtc)
                                        .TotalMilliseconds;
                                    _metrics.ObservationLatency(obsLatency);

                                    if (this.IsMatchObservationInteraction(currentInteraction.EnqueuedTimeUtc,
                                            observation.EnqueuedTimeUtc))
                                    {
                                        joinedInteraction = true;
                                        _metrics.JoinedObservationWithCurrentInteraction();
                                        joinedEvent.Add(observation);
                                    }
                                    else
                                    {
                                        // it timed out...
                                        _metrics.ObservationsEvicted(1);
                                        evictedDanglingObservations++;
                                        if (_invalidMessages != null)
                                        {
                                            await _invalidMessages?.SendAsync(new InvalidMessage()
                                            {
                                                Message = observation,
                                                Reason = InvalidMessageReason.EvictedObservation
                                            }, cancellationToken);
                                        }
                                    }
                                }
                                else
                                {
                                    // save the unmatched observation in case its matching interaction arrives later than when we examine the observation
                                    bool hasMatchingList = this._danglingObservationMap.TryGetValue(observation.EventId,
                                        out var existingDanglingObservationList);
                                    if (hasMatchingList)
                                    {
                                        existingDanglingObservationList.Add(observation);
                                    }
                                    else
                                    {
                                        existingDanglingObservationList = new List<Message>();
                                        existingDanglingObservationList.Add(observation);
                                        this._danglingObservationMap.Add(observation.EventId,
                                            existingDanglingObservationList);
                                    }
                                }
                            } while (currentRewardExpectDateTime < this.InteractionReadyTimestamp); // observation happened before interactionReadyTimestamp
                        } /* this.InteractionReadyTimestamp > this.LastObservationTimestamp */

                        // At this point historical observations have been inspected and new observations have been pulled up until the interactionReadyTimestamp.
                        if (joinedInteraction)
                        {
                            _metrics.JoinedInteractionWithObservation();
                        }
                        else
                        {
                            _metrics.NoMatchingObservationForInteraction();
                        }

                        joinedEvents.Messages.Add(joinedEvent);
                    } /* foreach (var current in interaction) */ /* Done processing/trying to join current interaction */


                    // The reason we send an entire MessageBatchs worth of events is because all interactions in the group share the same enqueued timestamp, this simplifies the checkpointing logic.
                    await this._output.SendAsync(joinedEvents, cancellationToken);

                    DateTime curTime = _timeProvider.UtcNow;
                    TimeSpan joinerDelayFromRank = curTime.Subtract(interaction.EnqueuedTimeUtc)
                        .Subtract(this._experimentalUnitDuration);
                    _metrics.JoinerDelayFromEnqueuedTime(joinerDelayFromRank.TotalSeconds);

                    // eviction
                    await this.EvictObservationsAsync(cancellationToken);
                }
            }
            catch (TaskCanceledException e)
            {

                this._logger?.LogError(e, "TaskCanceledException Exiting LeftOuterJoinBlock RunAsync()");
                throw;
            }
            catch (AggregateException ex)
            {
                this._logger?.LogError(ex, "{EventKey} {ErrorCode}", "LOJ.RunAsync.Exit",
                    PersonalizerInternalErrorCode.JoinerExecutionFailure.ToString());

                // ignore everything except taskcancellation
                ex = ex.Flatten();
                foreach (var e in ex.InnerExceptions)
                {
                    if (e is TaskCanceledException)
                        continue;

                    this._logger?.LogError(ex, "LOJ.RunAsync.Exit");
                }

                throw;
            }
            catch (Exception ex)
            {
                this._logger?.LogError(ex, "{EventKey} {ErrorCode}", "LOJ.RunAsync.Exit",
                    PersonalizerInternalErrorCode.JoinerExecutionFailure.ToString());
                throw;
            }
            finally
            {
                this._logger?.LogInformation("finally Exiting LeftOuterJoinBlock RunAsync()");
                // forward completion
                this._output.Complete();
            }
        }

        private async Task EvictObservationsAsync(CancellationToken cancellationToken)
        {
            DateTime oldestValidTimestamp;

            // Since we have to set initial value of oldestValidTimestamp to be DateTime.MinValue, take care of the overflow case.
            if (this.LastInteractionProcessedTimestamp <= DateTime.MinValue + this._backwardEventJoinWindowTimeSpan)
            {
                oldestValidTimestamp = DateTime.MinValue;
            }
            else
            {
                oldestValidTimestamp =
                    this.LastInteractionProcessedTimestamp.Subtract(this._backwardEventJoinWindowTimeSpan);
            }

            var evictedObservations = new List<Message>();
            foreach (var eventId in _danglingObservationMap.Keys)
            {
                var current = _danglingObservationMap[eventId];
                for (int i = 0; i < current.Count; i++)
                {
                    if (current[i].EnqueuedTimeUtc < oldestValidTimestamp)
                    {
                        evictedObservations.Add(current[i]);
                        current.RemoveAt(i);
                        i--;
                    }
                }

                if (current.Count == 0)
                {
                    _danglingObservationMap.Remove(eventId);
                }
            }

            // TODO, send all and await resulting list
            foreach (var evictedObservation in evictedObservations)
            {
                if (_invalidMessages != null)
                {
                    this._invalidMessages?.SendAsync(new InvalidMessage()
                    {
                        Message = evictedObservation,
                        Reason = InvalidMessageReason.EvictedObservation
                    }, cancellationToken);
                }
            }

            // tracking the number of evicted observations
            if (evictedObservations.Count > 0)
            {
                _metrics.DanglingObservations(this.DanglingObservationsCount);
                _metrics.ObservationsEvicted(evictedObservations.Count);
                evictedDanglingObservations += evictedObservations.Count;
            }
        }

        /// <summary>
        /// This methods encapsulates adjusting a given time by punctuation slack.
        /// There are tradeoffs in whether we use the slower, additive approach or the
        /// faster current time approach.
        /// The algo used is adjusted with AddPunctuationSlack option.
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        private DateTime AdjustByPunctuationSlack(DateTime time)
        {
            DateTime subtractivePunctuationTime = _timeProvider.UtcNow - this._punctuationSlack;
            if (this._config.AddPunctuationSlack)
            {
                DateTime additivePunctuationTime = time.Add(this._punctuationSlack);
                // Slower to give up trying to get/process events, but better for loops with consistent traffic.
                // Take min so we aren't more aggressive than the subtractivPunctuationTime.
                return new DateTime(Math.Min(subtractivePunctuationTime.Ticks, additivePunctuationTime.Ticks));
            }
            else
            {
                // Skips more data but better for loops that might stop sending events.
                return subtractivePunctuationTime;
            }
        }
    }
}