// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Google.FlatBuffers;
using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Trainer.FlatBuffers;
using reinforcement_learning.messages.flatbuff.v2;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

using fbv2 = reinforcement_learning.messages.flatbuff.v2;

// Builder classes for creating flatbuffer objects for testing
namespace CommonTest.Messages
{
    public class MetadataBuilder
    {
        private string _id;
        private DateTime _clientTimeUtc;
        private string _appId;
        private PayloadType _payloadType = PayloadType.CB;
        private float _passProbability;
        private EventEncoding _encoding = EventEncoding.Identity;

        public MetadataBuilder SetId(string id)
        {
            _id = id;
            return this;
        }

        public MetadataBuilder SetClientTimeUtc(DateTime clientTimeUtc)
        {
            _clientTimeUtc = clientTimeUtc;
            return this;
        }

        public MetadataBuilder SetAppId(string appId)
        {
            this._appId = appId;
            return this;
        }

        public MetadataBuilder SetPayloadType(PayloadType payloadType)
        {
            this._payloadType = payloadType;
            return this;
        }

        public MetadataBuilder SetPassProbability(float passProbability)
        {
            _passProbability = passProbability;
            return this;
        }

        public MetadataBuilder SetEncoding(EventEncoding encoding)
        {
            _encoding = encoding;
            return this;
        }

        public Offset<Metadata> Build(FlatBufferBuilder builder)
        {
            var idOff = builder.CreateString(_id);
            var appIdOff = builder.CreateString(_appId);

            Metadata.StartMetadata(builder);
            Metadata.AddId(builder, idOff);
            Metadata.AddEncoding(builder, _encoding);
            Metadata.AddPayloadType(builder, _payloadType);
            Metadata.AddAppId(builder, appIdOff);
            Metadata.AddPassProbability(builder, 0.5f);
            Metadata.AddClientTimeUtc(builder, _clientTimeUtc.SerializeV2TimeStamp(builder));
            return Metadata.EndMetadata(builder);
        }
    }

    public class EventBuilder
    {
        private byte[] _payload;

        public MetadataBuilder Meta { get; } = new MetadataBuilder();

        public EventBuilder SetPayload(byte[] payload)
        {
            _payload = payload;
            return this;
        }

        public Offset<Event> Build(FlatBufferBuilder builder, Offset<Metadata> metadata)
        {
            var payloadOff = Event.CreatePayloadVector(builder, _payload);
            Event.StartEvent(builder);
            Event.AddMeta(builder, metadata);
            Event.AddPayload(builder, payloadOff);
            return Event.EndEvent(builder);
        }
    }

    public class BatchMetadataBuilder
    {
        private string _contentEncodin = ApplicationConstants.IdentityBatch;
        private ulong _originalEventCount;

        public BatchMetadataBuilder SetContentEncoding(string contentEncoding)
        {
            _contentEncodin = contentEncoding;
            return this;
        }

        public BatchMetadataBuilder SetOriginalEventCount(ulong originalEventCount)
        {
            _originalEventCount = originalEventCount;
            return this;
        }

        public Offset<BatchMetadata> Build(FlatBufferBuilder builder)
        {
            var contentEncodingOff = builder.CreateString(_contentEncodin);
            BatchMetadata.StartBatchMetadata(builder);
            BatchMetadata.AddContentEncoding(builder, contentEncodingOff);
            BatchMetadata.AddOriginalEventCount(builder, _originalEventCount);
            return BatchMetadata.EndBatchMetadata(builder);
        }
    }

    public class SerializedEventBuilder
    {
        private byte[] _payload;

        public SerializedEventBuilder SetPayload(byte[] payload)
        {
            _payload = payload;
            return this;
        }

        public Offset<SerializedEvent> Build(FlatBufferBuilder builder)
        {
            var serializedEventsVectOff = SerializedEvent.CreatePayloadVector(builder, _payload);
            return SerializedEvent.CreateSerializedEvent(builder, payloadOffset: serializedEventsVectOff);
        }
    }

    public class EventBatchBuilder
    {
        private List<byte[]> _events = new();

        public BatchMetadataBuilder Meta { get; } = new BatchMetadataBuilder();

        public EventBatchBuilder AddSerializedEvent(byte[] eventBytes)
        {
            _events.Add(eventBytes);
            return this;
        }

        public Offset<EventBatch> Build(FlatBufferBuilder builder, Offset<BatchMetadata> batchMetadataOff)
        {
            var serializedEvents = new Offset<SerializedEvent>[_events.Count];
            var serializedEventBuilder = new SerializedEventBuilder();
            for (int i = 0; i < _events.Count; i++) {
                serializedEventBuilder.SetPayload(_events[i]);
                serializedEvents[i] = serializedEventBuilder.Build(builder);
            }
            var eventsVectOff = EventBatch.CreateEventsVector(builder, serializedEvents);
            EventBatch.StartEventBatch(builder);
            EventBatch.AddEvents(builder, eventsVectOff);
            EventBatch.AddMetadata(builder, batchMetadataOff);
            return EventBatch.EndEventBatch(builder);
        }
    }

    public class OutcomeEventBuilder
    {

        private object _value;
        private object _index;
        private bool _actionTaken;

        public OutcomeEventBuilder SetValue(string value)
        {
            _value = value;
            return this;
        }

        public OutcomeEventBuilder SetValue(float value)
        {
            _value = value;
            return this;
        }

        public OutcomeEventBuilder SetIndex(string value)
        {
            _index = value;
            return this;
        }

        public OutcomeEventBuilder SetIndex(int value)
        {
            _index = value;
            return this;
        }
        public OutcomeEventBuilder SetActionTaken(bool actionTaken)
        {
            _actionTaken = actionTaken;
            return this;
        }

        public Offset<OutcomeEvent> Build(FlatBufferBuilder builder)
        {
            StringOffset valueStringOff = (_value is string) ? builder.CreateString((string)_value) : default;
            Offset<NumericOutcome> numericValueOff = (_value is float) ? NumericOutcome.CreateNumericOutcome(builder, (float)_value) : default;
            StringOffset indexStringOff = (_index is string) ? builder.CreateString((string)_index) : default;
            Offset<NumericIndex> numericIndexOff = (_index is int) ? NumericIndex.CreateNumericIndex(builder, (int)_index) : default;

            OutcomeEvent.StartOutcomeEvent(builder);
            if (_value is string)
            {
                OutcomeEvent.AddValue(builder, valueStringOff.Value);
                OutcomeEvent.AddValueType(builder, OutcomeValue.literal);
            }
            else if (_value is float)
            {
                OutcomeEvent.AddValue(builder, numericValueOff.Value);
                OutcomeEvent.AddValueType(builder, OutcomeValue.numeric);
            }
            else
            {
                OutcomeEvent.AddValueType(builder, OutcomeValue.NONE);
            }
            if (_index is string)
            {
                OutcomeEvent.AddIndex(builder, indexStringOff.Value);
                OutcomeEvent.AddIndexType(builder, IndexValue.literal);
            }
            else if (_index is int)
            {
                OutcomeEvent.AddIndex(builder, numericIndexOff.Value);
                OutcomeEvent.AddIndexType(builder, IndexValue.numeric);
            }
            else
            {
                OutcomeEvent.AddIndexType(builder, IndexValue.NONE);
            }
            OutcomeEvent.AddActionTaken(builder, _actionTaken);
            return OutcomeEvent.EndOutcomeEvent(builder);
        }
    }

    public class CaEventBuilder
    {
        private bool _deferredAction;
        private float _action;
        private byte[] _context;
        private float _pdfValue;
        private string _modelId;
        private LearningModeType _learningMode;

        public CaEventBuilder SetDeferredAction(bool deferredAction)
        {
            _deferredAction = deferredAction;
            return this;
        }

        public CaEventBuilder SetAction(float action)
        {
            _action = action;
            return this;
        }

        public CaEventBuilder SetContext(byte[] context)
        {
            _context = context;
            return this;
        }

        public CaEventBuilder SetPdfValue(float pdfValue)
        {
            _pdfValue = pdfValue;
            return this;
        }

        public CaEventBuilder SetModelId(string modelId)
        {
            _modelId = modelId;
            return this;
        }

        public CaEventBuilder SetLearningMode(LearningModeType learningMode)
        {
            _learningMode = learningMode;
            return this;
        }

        public Offset<CaEvent> Build(FlatBufferBuilder builder)
        {
            var contextOff = CaEvent.CreateContextVector(builder, _context);
            var modelIdOff = builder.CreateString(_modelId);

            CaEvent.StartCaEvent(builder);
            CaEvent.AddDeferredAction(builder, _deferredAction);
            CaEvent.AddAction(builder, _action);
            CaEvent.AddContext(builder, contextOff);
            CaEvent.AddPdfValue(builder, _pdfValue);
            CaEvent.AddModelId(builder, modelIdOff);
            CaEvent.AddLearningMode(builder, _learningMode);
            return CaEvent.EndCaEvent(builder);
        }
    }

    public class CbEventBuilder
    {
        private bool _deferredAction;
        private ulong[] _actionIds;
        private byte[] _context;
        private float[] _probabilities;
        private string _modelId;
        private LearningModeType _learningMode;

        public CbEventBuilder SetDeferredAction(bool deferredAction)
        {
            _deferredAction = deferredAction;
            return this;
        }

        public CbEventBuilder SetActionIds(ulong[] actionIds)
        {
            _actionIds = actionIds;
            return this;
        }

        public CbEventBuilder SetContext(byte[] context)
        {
            _context = context;
            return this;
        }

        public CbEventBuilder SetProbabilities(float[] probabilities)
        {
            _probabilities = probabilities;
            return this;
        }

        public CbEventBuilder SetModelId(string modelId)
        {
            _modelId = modelId;
            return this;
        }

        public CbEventBuilder SetLearningMode(LearningModeType learningMode)
        {
            _learningMode = learningMode;
            return this;
        }

        public Offset<CbEvent> Build(FlatBufferBuilder builder)
        {
            var actionIdsOff = CbEvent.CreateActionIdsVector(builder, _actionIds);
            var probabilitiesOff = CbEvent.CreateProbabilitiesVector(builder, _probabilities);
            var contextOff = CbEvent.CreateContextVector(builder, _context);
            var modelIdOff = builder.CreateString(_modelId);

            CbEvent.StartCbEvent(builder);
            CbEvent.AddDeferredAction(builder, _deferredAction);
            CbEvent.AddActionIds(builder, actionIdsOff);
            CbEvent.AddContext(builder, contextOff);
            CbEvent.AddProbabilities(builder, probabilitiesOff);
            CbEvent.AddModelId(builder, modelIdOff);
            CbEvent.AddLearningMode(builder, _learningMode);
            return CbEvent.EndCbEvent(builder);
        }
    }

    public class DedupInfoBuidler
    {
        private ulong[] _ids;
        private string[] _values;


        public DedupInfoBuidler SetIds(ulong[] ids)
        {
            _ids = ids;
            return this;
        }

        public DedupInfoBuidler SetValues(string[] values)
        {
            _values = values;
            return this;
        }

        public Offset<DedupInfo> Build(FlatBufferBuilder builder)
        {
            var idsOff = DedupInfo.CreateIdsVector(builder, _ids);
            var valuesOff = new StringOffset[_values.Length];
            for (int i = 0; i < _values.Length; i++) {
                valuesOff[i] = builder.CreateString(_values[i]);
            }
            var valuesVectOff = DedupInfo.CreateValuesVector(builder, valuesOff);

            DedupInfo.StartDedupInfo(builder);
            DedupInfo.AddIds(builder, idsOff);
            DedupInfo.AddValues(builder, valuesVectOff);
            return DedupInfo.EndDedupInfo(builder);
        }
    }

    public class SlotEventBuilder
    {
        private uint[] _actionIds;
        private float[] _probabilities;
        private string _id;

        public SlotEventBuilder SetActionIds(uint[] actionIds)
        {
            _actionIds = actionIds;
            return this;
        }

        public SlotEventBuilder SetProbabilities(float[] probabilities)
        {
            _probabilities = probabilities;
            return this;
        }

        public SlotEventBuilder SetId(string id)
        {
            _id = id;
            return this;
        }

        public Offset<SlotEvent> Build(FlatBufferBuilder builder)
        {
            var actionIdsOff = SlotEvent.CreateActionIdsVector(builder, _actionIds);
            var probabilitiesOff = SlotEvent.CreateProbabilitiesVector(builder, _probabilities);
            var idOff = builder.CreateString(_id);

            SlotEvent.StartSlotEvent(builder);
            SlotEvent.AddActionIds(builder, actionIdsOff);
            SlotEvent.AddProbabilities(builder, probabilitiesOff);
            SlotEvent.AddId(builder, idOff);
            return SlotEvent.EndSlotEvent(builder);
        }
    }

    public class MultiSlotEventBuilder
    {
        private readonly List<Offset<SlotEvent>> _slotEvents = new();
        private byte[] _context;
        private string _modelId;
        private bool _deferredAction;
        private int[] baselineActions;
        private LearningModeType _learningMode;

        public SlotEventBuilder Slots { get; } = new SlotEventBuilder();

        public MultiSlotEventBuilder AddSlotEvent(Offset<SlotEvent> slotEvent)
        {
            _slotEvents.Add(slotEvent);
            return this;
        }

        public MultiSlotEventBuilder AddSlotEvent(IList<Offset<SlotEvent>> slotEvents)
        {
            _slotEvents.AddRange(slotEvents);
            return this;
        }

        public MultiSlotEventBuilder SetContext(byte[] context)
        {
            _context = context;
            return this;
        }

        public MultiSlotEventBuilder SetModelId(string modelId)
        {
            _modelId = modelId;
            return this;
        }

        public MultiSlotEventBuilder SetDeferredAction(bool deferredAction)
        {
            _deferredAction = deferredAction;
            return this;
        }

        public MultiSlotEventBuilder SetBaselineActions(int[] baselineActions)
        {
            this.baselineActions = baselineActions;
            return this;
        }

        public MultiSlotEventBuilder SetLearningMode(LearningModeType learningMode)
        {
            _learningMode = learningMode;
            return this;
        }

        public Offset<MultiSlotEvent> Build(FlatBufferBuilder builder)
        {
            var contextOff = MultiSlotEvent.CreateContextVector(builder, _context);
            var modelIdOff = builder.CreateString(_modelId);
            var baselineActionsOff = MultiSlotEvent.CreateBaselineActionsVector(builder, baselineActions);
            var slotsVectOff = MultiSlotEvent.CreateSlotsVector(builder, _slotEvents.ToArray());

            MultiSlotEvent.StartMultiSlotEvent(builder);
            MultiSlotEvent.AddSlots(builder, slotsVectOff);
            MultiSlotEvent.AddContext(builder, contextOff);
            MultiSlotEvent.AddModelId(builder, modelIdOff);
            MultiSlotEvent.AddDeferredAction(builder, _deferredAction);
            MultiSlotEvent.AddBaselineActions(builder, baselineActionsOff);
            MultiSlotEvent.AddLearningMode(builder, _learningMode);
            return MultiSlotEvent.EndMultiSlotEvent(builder);
        }
    }

    public class MultiStepEventBuilder
    {
        private string _eventId;
        private string _previousId;
        private ulong[] _actionIds;
        private byte[] _context;
        private float[] _probabilities;
        private string _modelId;
        private bool _deferredAction;

        public MultiStepEventBuilder SetEventId(string eventId)
        {
            _eventId = eventId;
            return this;
        }

        public MultiStepEventBuilder SetPreviousId(string previousId)
        {
            _previousId = previousId;
            return this;
        }

        public MultiStepEventBuilder SetActionIds(ulong[] actionIds)
        {
            _actionIds = actionIds;
            return this;
        }

        public MultiStepEventBuilder SetContext(byte[] context)
        {
            _context = context;
            return this;
        }

        public MultiStepEventBuilder SetProbabilities(float[] probabilities)
        {
            _probabilities = probabilities;
            return this;
        }

        public MultiStepEventBuilder SetModelId(string modelId)
        {
            _modelId = modelId;
            return this;
        }

        public MultiStepEventBuilder SetDeferredAction(bool deferredAction)
        {
            _deferredAction = deferredAction;
            return this;
        }

        public Offset<MultiStepEvent> Build(FlatBufferBuilder builder)
        {
            var eventIdOff = builder.CreateString(_eventId);
            var previousIdOff = builder.CreateString(_previousId);
            var actionIdsOff = MultiStepEvent.CreateActionIdsVector(builder, _actionIds);
            var contextOff = MultiStepEvent.CreateContextVector(builder, _context);
            var probabilitiesOff = MultiStepEvent.CreateProbabilitiesVector(builder, _probabilities);
            var modelIdOff = builder.CreateString(_modelId);

            MultiStepEvent.StartMultiStepEvent(builder);
            MultiStepEvent.AddEventId(builder, eventIdOff);
            MultiStepEvent.AddPreviousId(builder, previousIdOff);
            MultiStepEvent.AddActionIds(builder, actionIdsOff);
            MultiStepEvent.AddContext(builder, contextOff);
            MultiStepEvent.AddProbabilities(builder, probabilitiesOff);
            MultiStepEvent.AddModelId(builder, modelIdOff);
            MultiStepEvent.AddDeferredAction(builder, _deferredAction);
            return MultiStepEvent.EndMultiStepEvent(builder);
        }
    }

    public class EpisodeEventBuilder
    {
        private string _episodeId;

        public EpisodeEventBuilder SetEpisodeId(string episodeId)
        {
            _episodeId = episodeId;
            return this;
        }

        public Offset<EpisodeEvent> Build(FlatBufferBuilder builder)
        {
            var episodeIdOff = builder.CreateString(_episodeId);

            EpisodeEvent.StartEpisodeEvent(builder);
            EpisodeEvent.AddEpisodeId(builder, episodeIdOff);
            return EpisodeEvent.EndEpisodeEvent(builder);
        }
    }

    public class CheckpointInfoBuilder
    {
        private RewardFunctionType _rewardFunctionType;
        private float _defaultReward;
        private LearningModeType? _learningModeConfig;
        private fbv2.ProblemType? _problemTypeConfig;
        private bool? _useClientTime;

        public CheckpointInfoBuilder SetRewardFunctionType(RewardFunctionType rewardType)
        {
            _rewardFunctionType = rewardType;
            return this;
        }

        public CheckpointInfoBuilder SetDefaultReward(float defaultReward)
        {
            _defaultReward = defaultReward;
            return this;
        }

        public CheckpointInfoBuilder SetLearningModeConfig(LearningModeType learningModeConfig)
        {
            _learningModeConfig = learningModeConfig;
            return this;
        }

        public CheckpointInfoBuilder SetProblemTypeConfig(fbv2.ProblemType problemTypeConfig)
        {
            this._problemTypeConfig = problemTypeConfig;
            return this;
        }

        public CheckpointInfoBuilder SetUseClientTime(bool useClientTime)
        {
            this._useClientTime = useClientTime;
            return this;
        }

        public Offset<fbv2.CheckpointInfo> Build(FlatBufferBuilder builder)
        {
            fbv2.CheckpointInfo.StartCheckpointInfo(builder);
            fbv2.CheckpointInfo.AddRewardFunctionType(builder, _rewardFunctionType);
            fbv2.CheckpointInfo.AddDefaultReward(builder, _defaultReward);
            if (_learningModeConfig.HasValue)
            {
                fbv2.CheckpointInfo.AddLearningModeConfig(builder, _learningModeConfig.Value);
            }
            if (_problemTypeConfig.HasValue)
            {
                fbv2.CheckpointInfo.AddProblemTypeConfig(builder, _problemTypeConfig.Value);
            }
            if (_useClientTime.HasValue)
            {
                fbv2.CheckpointInfo.AddUseClientTime(builder, _useClientTime.Value);
            }
            return fbv2.CheckpointInfo.EndCheckpointInfo(builder);
        }
    }

    public class FBMessageBuilder
    {
        public enum ContentEncoding
        {
            IDENTITY,
            DEDUP,
        }

        public class SlotData
        {
            public uint[] ActionIds { get; set; }
            public float[] Probabilities { get; set; }
            public string Id { get; set; }
        }

        public static OutcomeEvent CreateOutcome(string value, string index, bool actionTaken)
        {
            var fbb = new FlatBufferBuilder(128);
            OutcomeEventBuilder builder = new();
            var outcome = builder
                .SetValue(value)
                .SetIndex(index)
                .SetActionTaken(actionTaken)
                .Build(fbb);
            fbb.Finish(outcome.Value);
            return OutcomeEvent.GetRootAsOutcomeEvent(fbb.DataBuffer);
        }

        public static OutcomeEvent CreateOutcome(float value, int index, bool actionTaken)
        {
            var fbb = new FlatBufferBuilder(128);
            OutcomeEventBuilder builder = new();
            var outcome = builder
                .SetValue(value)
                .SetIndex(index)
                .SetActionTaken(actionTaken)
                .Build(fbb);
            fbb.Finish(outcome.Value);
            return OutcomeEvent.GetRootAsOutcomeEvent(fbb.DataBuffer);
        }

        public static OutcomeEvent CreateOutcome(bool actionTaken)
        {
            var fbb = new FlatBufferBuilder(128);
            OutcomeEventBuilder builder = new();
            var outcome = builder
                .SetActionTaken(actionTaken)
                .Build(fbb);
            fbb.Finish(outcome.Value);
            return OutcomeEvent.GetRootAsOutcomeEvent(fbb.DataBuffer);
        }

        public static CaEvent CreateCaEvent(bool deferredAction, float action, byte[] context, float pdfValue, string modelId, LearningModeType mode)
        {
            var fbb = new FlatBufferBuilder(128);
            CaEventBuilder builder = new();
            var caEvent = builder
                .SetDeferredAction(deferredAction)
                .SetAction(action)
                .SetContext(context)
                .SetPdfValue(pdfValue)
                .SetModelId(modelId)
                .SetLearningMode(mode)
                .Build(fbb);
            fbb.Finish(caEvent.Value);
            return CaEvent.GetRootAsCaEvent(fbb.DataBuffer);
        }

        public static CbEvent CreateCbEvent(bool deferredAction, ulong[] actionIds, byte[] context, float[] probabilities, string modelId, LearningModeType mode)
        {
            var fbb = new FlatBufferBuilder(128);
            CbEventBuilder builder = new();
            var cbEvent = builder
                .SetDeferredAction(deferredAction)
                .SetActionIds(actionIds)
                .SetContext(context)
                .SetProbabilities(probabilities)
                .SetModelId(modelId)
                .SetLearningMode(mode)
                .Build(fbb);
            fbb.Finish(cbEvent.Value);
            return CbEvent.GetRootAsCbEvent(fbb.DataBuffer);
        }

        public static DedupInfo CreateDedupInfo(ulong[] ids, string[] values)
        {
            var fbb = new FlatBufferBuilder(128);
            DedupInfoBuidler builder = new();
            var dedupInfo = builder
                .SetIds(ids)
                .SetValues(values)
                .Build(fbb);
            fbb.Finish(dedupInfo.Value);
            return DedupInfo.GetRootAsDedupInfo(fbb.DataBuffer);
        }

        public static MultiSlotEvent CreateMultiSlotEvent(SlotData[] slotEvents, byte[] context, string modelId, bool deferredAction, int[] baselineActions, LearningModeType mode)
        {
            var fbb = new FlatBufferBuilder(128);
            MultiSlotEventBuilder builder = new();
            foreach (var slotEvent in slotEvents)
            {
                builder.Slots
                    .SetActionIds(slotEvent.ActionIds)
                    .SetProbabilities(slotEvent.Probabilities)
                    .SetId(slotEvent.Id);
                builder.AddSlotEvent(builder.Slots.Build(fbb));
            }
            var multiSlotEvent = builder
                .SetContext(context)
                .SetModelId(modelId)
                .SetDeferredAction(deferredAction)
                .SetBaselineActions(baselineActions)
                .SetLearningMode(mode)
                .Build(fbb);
            fbb.Finish(multiSlotEvent.Value);
            return MultiSlotEvent.GetRootAsMultiSlotEvent(fbb.DataBuffer);
        }

        public static MultiStepEvent CreateMultiStepEvent(string eventId, string previousId, ulong[] actionIds, byte[] context, float[] probabilities, string modelId, bool deferredAction)
        {
            var fbb = new FlatBufferBuilder(128);
            MultiStepEventBuilder builder = new();
            var multiStepEvent = builder
                .SetEventId(eventId)
                .SetPreviousId(previousId)
                .SetActionIds(actionIds)
                .SetContext(context)
                .SetProbabilities(probabilities)
                .SetModelId(modelId)
                .SetDeferredAction(deferredAction)
                .Build(fbb);
            fbb.Finish(multiStepEvent.Value);
            return MultiStepEvent.GetRootAsMultiStepEvent(fbb.DataBuffer);
        }

        public static EpisodeEvent CreateEpisodeEvent(string episodeId)
        {
            var fbb = new FlatBufferBuilder(128);
            EpisodeEventBuilder builder = new();
            var episodeEvent = builder
                .SetEpisodeId(episodeId)
                .Build(fbb);
            fbb.Finish(episodeEvent.Value);
            return EpisodeEvent.GetRootAsEpisodeEvent(fbb.DataBuffer);
        }

        public static Event CreateEventAsSlates(string appId, string eventId, DateTime clientTimeUTC, EventEncoding encoding, float passProbability, MultiSlotEvent fbEvent)
        {
            return CreateEvent(appId, eventId, clientTimeUTC, encoding, passProbability, fbEvent, PayloadType.Slates);
        }

        public static Event CreateEventAsCCB(string appId, string eventId, DateTime clientTimeUTC, EventEncoding encoding, float passProbability, MultiSlotEvent fbEvent)
        {
            return CreateEvent(appId, eventId, clientTimeUTC, encoding, passProbability, fbEvent, PayloadType.CCB);
        }

        public static Event CreateEvent(string appId, string eventId, DateTime clientTimeUTC, EventEncoding encoding, float passProbability, IFlatbufferObject fbEvent)
        {
            var payloadType = EnsureValidEventType(fbEvent);
            return CreateEvent(appId, eventId, clientTimeUTC, encoding, passProbability, fbEvent, payloadType);
        }

        private static Event CreateEvent(string appId, string eventId, DateTime clientTimeUTC, EventEncoding encoding, float passProbability, IFlatbufferObject fbEvent, PayloadType fbType)
        {
            Contract.Requires(encoding == EventEncoding.Identity, "only Identity encoding is support at this time");
            Contract.Requires(fbEvent != null);
            var payloadType = fbType;
            var eventFbb = new FlatBufferBuilder(128);
            EventBuilder eventBuilder = new();
            var theEvent = eventBuilder
                .SetPayload(fbEvent.ByteBuffer.ToSizedArray())
                .Build(eventFbb, eventBuilder.Meta
                    .SetId(eventId)
                    .SetAppId(appId)
                    .SetClientTimeUtc(clientTimeUTC)
                    .SetEncoding(encoding)
                    .SetPassProbability(passProbability)
                    .SetPayloadType(payloadType)
                    .Build(eventFbb)
                );
            eventFbb.Finish(theEvent.Value);
            return Event.GetRootAsEvent(eventFbb.DataBuffer);
        }

        public static EventBatch CreateEventBatch(IList<Event> events, ContentEncoding contentEncoding = ContentEncoding.IDENTITY)
        {
            var batchFbb = new FlatBufferBuilder(128);
            EventBatchBuilder batchBuilder = new();
            foreach (var e in events)
            {
                batchBuilder.AddSerializedEvent(e.ByteBuffer.ToSizedArray());
            }
            var batchOff = batchBuilder
                .Build(batchFbb, batchBuilder.Meta
                    .SetContentEncoding(contentEncoding.ToString())
                    .SetOriginalEventCount((ulong)events.Count)
                    .Build(batchFbb)
                );
            batchFbb.Finish(batchOff.Value);
            return EventBatch.GetRootAsEventBatch(batchFbb.DataBuffer);
        }

        public static fbv2.CheckpointInfo CreateCheckpointInfo(float defaultReward, fbv2.RewardFunctionType rewardFnType, fbv2.ProblemType? problemType = null, fbv2.LearningModeType? learningMode = null, bool? useClientTime = null)
        {
            var fbb = new FlatBufferBuilder(128);
            var builder = new CheckpointInfoBuilder();
            builder.SetDefaultReward(defaultReward).SetRewardFunctionType(rewardFnType);
            if (problemType.HasValue)
            {
                builder.SetProblemTypeConfig(problemType.Value);
            }
            if (learningMode.HasValue)
            {
                builder.SetLearningModeConfig(learningMode.Value);
            }
            if (useClientTime.HasValue)
            {
                builder.SetUseClientTime(useClientTime.Value);
            }
            var checkpointOffset = builder.Build(fbb);
            fbb.Finish(checkpointOffset.Value);
            return fbv2.CheckpointInfo.GetRootAsCheckpointInfo(fbb.DataBuffer);

        }

        private static PayloadType EnsureValidEventType(IFlatbufferObject e)
        {
            Contract.Requires(e != null);
            if (e is OutcomeEvent)
            {
                return PayloadType.Outcome;
            }
            if (e is CaEvent)
            {
                return PayloadType.CA;
            }
            if (e is CbEvent)
            {
                return PayloadType.CB;
            }
            if (e is DedupInfo)
            {
                return PayloadType.DedupInfo;
            }
            if (e is MultiSlotEvent)
            {
                throw new ArgumentException($"Invalid event type: {e.GetType().Name}");
            }
            if (e is MultiStepEvent)
            {
                return PayloadType.MultiStep;
            }
            if (e is EpisodeEvent)
            {
                return PayloadType.Episode;
            }
            throw new ArgumentException($"Invalid event type: {e.GetType().Name}");
        }
    }
}
