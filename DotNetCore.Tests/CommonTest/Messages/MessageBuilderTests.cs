// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommonTest.Messages;
using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Trainer.Data;
using Microsoft.DecisionService.OnlineTrainer.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using reinforcement_learning.messages.flatbuff.v2;
using System;
using System.Linq;

namespace DotNetCore.Tests.CommonTest.Messages
{
    [TestClass]
    public class MessageBuilderTests
    {
        [TestMethod]
        [Description("successfully create an EventBatch of several event messages and parse them using SchemaV2Parser")]
        public void CreateEventBatch()
        {
            var appId = "test-app-id";
            var events = new Event[] {
                FBMessageBuilder.CreateEvent(appId, "event-id-1", DateTime.UtcNow, EventEncoding.Identity, 1.0f,
                    FBMessageBuilder.CreateOutcome(0.6f, 1, false)
                ),
                FBMessageBuilder.CreateEvent(appId, "event-id-2", DateTime.UtcNow, EventEncoding.Identity, 1.0f,
                    FBMessageBuilder.CreateOutcome(0.7f, 2, false)
                ),
                FBMessageBuilder.CreateEvent(appId, "event-id-3", DateTime.UtcNow, EventEncoding.Identity, 1.0f,
                    FBMessageBuilder.CreateOutcome(0.7f, 2, false)
                ),
                FBMessageBuilder.CreateEvent(appId, "event-id-4", DateTime.UtcNow, EventEncoding.Identity, 1.0f,
                    FBMessageBuilder.CreateOutcome(0.7f, 2, false)
                ),
                FBMessageBuilder.CreateEvent(appId, "event-id-5", DateTime.UtcNow, EventEncoding.Identity, 1.0f,
                    FBMessageBuilder.CreateCaEvent(true, 0.5f, new byte[] { 1, 2, 3, 4 }, 0.5f, "test-model-1", LearningModeType.Online)
                ),
                FBMessageBuilder.CreateEvent(appId, "event-id-6", DateTime.UtcNow, EventEncoding.Identity, 1.0f,
                    FBMessageBuilder.CreateCbEvent(true, new ulong[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }, new float[] { 0.1f, 0.2f, 0.3f }, "test-model-1", LearningModeType.Online)
                ),
            };
            var eventBatch = FBMessageBuilder.CreateEventBatch(events);
            var eventData = EHMessageBuilder.CreateEvent(MessageType.FlatBuffGenericEventBatch, eventBatch);
            IMessageData messageData = new EventHubData(eventData, "part-1");
            var msgBatch = SchemaV2Parser.ProcessEventHubMessage(messageData);
            Assert.AreEqual(events.Length, msgBatch.Messages.Count);
            for (int i = 0; i < events.Length; i++)
            {
                var msg = msgBatch.Messages[i];
                Assert.AreEqual(events[i].Meta.Value.AppId, msg.AppId);
                Assert.AreEqual(events[i].Meta.Value.Id, msg.EventId);
                Assert.AreEqual(events[i].Meta.Value.PayloadType, msg.PayloadType);
            }
        }

        [TestMethod]
        [Description("ensure Event can be created for each event payload type")]
        public void CreateEvent()
        {
            var appId = "test-app-id";
            var outcome = FBMessageBuilder.CreateEvent(appId, "event-id-1", DateTime.UtcNow, EventEncoding.Identity, 1.0f,
                FBMessageBuilder.CreateOutcome(0.6f, 1, false)
            );
            Assert.AreEqual(outcome.Meta.Value.PayloadType, PayloadType.Outcome);

            var dedupinfo = FBMessageBuilder.CreateEvent(appId, "event-id-2", DateTime.UtcNow, EventEncoding.Identity, 1.0f,
                FBMessageBuilder.CreateDedupInfo(new ulong[] { 1, 2, 3, 4 }, new string[] { "1a", "2a", "3a", "4a" })
            );
            Assert.AreEqual(dedupinfo.Meta.Value.PayloadType, PayloadType.DedupInfo);

            var episode = FBMessageBuilder.CreateEvent(appId, "event-id-3", DateTime.UtcNow, EventEncoding.Identity, 1.0f,
                FBMessageBuilder.CreateEpisodeEvent("episode-1")
            );
            Assert.AreEqual(episode.Meta.Value.PayloadType, PayloadType.Episode);

            var multistep = FBMessageBuilder.CreateEvent(appId, "event-id-4", DateTime.UtcNow, EventEncoding.Identity, 1.0f,
                FBMessageBuilder.CreateMultiStepEvent("event-id-1", "event-id-0", new ulong[] { 1, 2, 3, 4 }, new byte[] { 1, 2, 3, 4 }, new float[] { 0.4f, 0.5f, 0.6f, 0.7f }, "model-id-1", false)
            );
            Assert.AreEqual(multistep.Meta.Value.PayloadType, PayloadType.MultiStep);

            var ca = FBMessageBuilder.CreateEvent(appId, "event-id-5", DateTime.UtcNow, EventEncoding.Identity, 1.0f,
                FBMessageBuilder.CreateCaEvent(true, 0.5f, new byte[] { 1, 2, 3, 4 }, 0.5f, "test-model-1", LearningModeType.Online)
            );
            Assert.AreEqual(ca.Meta.Value.PayloadType, PayloadType.CA);

            var cb = FBMessageBuilder.CreateEvent(appId, "event-id-6", DateTime.UtcNow, EventEncoding.Identity, 1.0f,
                FBMessageBuilder.CreateCbEvent(true, new ulong[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }, new float[] { 0.1f, 0.2f, 0.3f }, "test-model-1", LearningModeType.Online)
            );
            Assert.AreEqual(cb.Meta.Value.PayloadType, PayloadType.CB);

            var ccb = FBMessageBuilder.CreateEventAsCCB(appId, "event-id-6", DateTime.UtcNow, EventEncoding.Identity, 1.0f,
                FBMessageBuilder.CreateMultiSlotEvent(
                    new FBMessageBuilder.SlotData[] {
                    new() { ActionIds = new uint[] { 1, 2, 3 }, Id = "1a", Probabilities = new float[] { 0.1f, 0.45f, 0.2f } },
                    new() { ActionIds = new uint[] { 4, 5, 6 }, Id = "2a", Probabilities = new float[] { 0.11f, 0.55f, 0.25f } },
                    },
                    new byte[] { 1, 2, 3 },
                    "test-model-1",
                    false,
                    new int[] { 1, 2, 3 },
                    LearningModeType.Online
                )
            );
            Assert.AreEqual(ccb.Meta.Value.PayloadType, PayloadType.CCB);

            var slates = FBMessageBuilder.CreateEventAsSlates(appId, "event-id-6", DateTime.UtcNow, EventEncoding.Identity, 1.0f,
                FBMessageBuilder.CreateMultiSlotEvent(
                    new FBMessageBuilder.SlotData[] {
                    new() { ActionIds = new uint[] { 1, 2, 3 }, Id = "5a", Probabilities = new float[] { 0.2f, 0.25f, 0.42f } },
                    new() { ActionIds = new uint[] { 4, 5, 6 }, Id = "6a", Probabilities = new float[] { 0.11f, 0.15f, 0.25f } },
                    },
                    new byte[] { 6, 7, 8 },
                    "test-model-1",
                    false,
                    new int[] { 9, 8, 7 },
                    LearningModeType.Online
                )
            );
            Assert.AreEqual(slates.Meta.Value.PayloadType, PayloadType.Slates);
        }

        [TestMethod]
        [Description("sucessfully create Outcome")]
        public void CreateOutcome()
        {
            var e1 = FBMessageBuilder.CreateOutcome("1.23", "1", true);
            Assert.AreEqual("1.23", e1.ValueAsString());
            Assert.AreEqual("1", e1.IndexAsString());
            Assert.AreEqual(true, e1.ActionTaken);

            var e2 = FBMessageBuilder.CreateOutcome(1.23f, 1, true);
            Assert.AreEqual(1.23f, e2.ValueAsnumeric().Value);
            Assert.AreEqual(1, e2.IndexAsnumeric().Index);
            Assert.AreEqual(true, e2.ActionTaken);

            var e3 = FBMessageBuilder.CreateOutcome(false);
            Assert.AreEqual(OutcomeValue.NONE, e3.ValueType);
            Assert.AreEqual(IndexValue.NONE, e3.IndexType);
            Assert.AreEqual(false, e3.ActionTaken);
        }

        [TestMethod]
        [Description("sucessfully create CaEvent")]
        public void CreateCaEvent()
        {
            var e1 = FBMessageBuilder.CreateCaEvent(true, 1.4f, new byte[] { 1, 2, 3, 4 }, 0.5f, "test-model-1", LearningModeType.Online);
            Assert.AreEqual(true, e1.DeferredAction);
            Assert.AreEqual(1.4f, e1.Action);
            Assert.IsTrue(e1.GetContextArray().SequenceEqual(new byte[] { 1, 2, 3, 4 }));
            Assert.AreEqual(0.5f, e1.PdfValue);
            Assert.AreEqual(e1.ModelId, "test-model-1");
            Assert.AreEqual(LearningModeType.Online, e1.LearningMode);
        }

        [TestMethod]
        [Description("sucessfully create CbEvent")]
        public void CreateCbEvent()
        {
            var e1 = FBMessageBuilder.CreateCbEvent(true, new ulong[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }, new float[] { 0.1f, 0.2f, 0.3f }, "test-model-1", LearningModeType.Online);
            Assert.AreEqual(true, e1.DeferredAction);
            Assert.IsTrue(e1.GetActionIdsArray().SequenceEqual(new ulong[] { 1, 2, 3 }));
            Assert.IsTrue(e1.GetContextArray().SequenceEqual(new byte[] { 1, 2, 3 }));
            Assert.IsTrue(e1.GetProbabilitiesArray().SequenceEqual(new float[] { 0.1f, 0.2f, 0.3f }));
            Assert.AreEqual(e1.ModelId, "test-model-1");
            Assert.AreEqual(LearningModeType.Online, e1.LearningMode);
        }

        [TestMethod]
        [Description("sucessfully create Dedupinfo")]
        public void CreateDedupInfo()
        {
            var values = new string[] { "1", "2", "3" };
            var e1 = FBMessageBuilder.CreateDedupInfo(new ulong[] { 1, 2, 3 }, values);
            Assert.IsTrue(e1.GetIdsArray().SequenceEqual(new ulong[] { 1, 2, 3 }));
            Assert.AreEqual(values.Length, e1.ValuesLength);
            for (int i = 0; i < values.Length; i++)
            {
                Assert.AreEqual(values[i], e1.Values(i));
            }
        }

        [TestMethod]
        [Description("sucessfully create MultiSlotEvent")]
        public void CreateMultiSlotEvent()
        {
            var testSlots = new FBMessageBuilder.SlotData[] {
            new() { Id = "1", ActionIds = new uint[] { 1, 2, 3 }, Probabilities = new float[] { 0.1f, 0.3f, 0.6f } },
            new() { Id = "2", ActionIds = new uint[] { 4, 5, 6 }, Probabilities = new float[] { 0.12f, 0.32f, 0.62f } },
            new() { Id = "3", ActionIds = new uint[] { 7, 8, 9 }, Probabilities = new float[] { 0.14f, 0.34f, 0.64f } },
        };
            var e1 = FBMessageBuilder.CreateMultiSlotEvent(
                testSlots,
                new byte[] { 1, 2, 3 },
                "test-model-1",
                false,
                new int[] { 4, 5, 6 },
                LearningModeType.Online
            );
            Assert.AreEqual(e1.ModelId, "test-model-1");
            Assert.AreEqual(LearningModeType.Online, e1.LearningMode);
            Assert.IsTrue(e1.GetContextArray().SequenceEqual(new byte[] { 1, 2, 3 }));
            Assert.AreEqual(false, e1.DeferredAction);
            Assert.IsTrue(e1.GetBaselineActionsArray().SequenceEqual(new int[] { 4, 5, 6 }));
            Assert.AreEqual(testSlots.Length, e1.SlotsLength);
            for (int i = 0; i < testSlots.Length; i++)
            {
                var slot = e1.Slots(i);
                Assert.AreEqual(testSlots[i].Id, slot.Value.Id);
                Assert.IsTrue(testSlots[i].ActionIds.SequenceEqual(slot.Value.GetActionIdsArray()));
                Assert.IsTrue(testSlots[i].Probabilities.SequenceEqual(slot.Value.GetProbabilitiesArray()));
            }
        }

        [TestMethod]
        [Description("sucessfully create MultiStepEvent")]
        public void CreateMultiStepEvent()
        {
            var e1 = FBMessageBuilder.CreateMultiStepEvent("event-id-1", "event-id-0", new ulong[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }, new float[] { 0.1f, 0.2f, 0.5f }, "model-id-1", false);
            Assert.AreEqual("event-id-1", e1.EventId);
            Assert.AreEqual("event-id-0", e1.PreviousId);
            Assert.IsTrue(e1.GetActionIdsArray().SequenceEqual(new ulong[] { 1, 2, 3 }));
            Assert.IsTrue(e1.GetContextArray().SequenceEqual(new byte[] { 1, 2, 3 }));
            Assert.IsTrue(e1.GetProbabilitiesArray().SequenceEqual(new float[] { 0.1f, 0.2f, 0.5f }));
            Assert.AreEqual("model-id-1", e1.ModelId);
            Assert.AreEqual(false, e1.DeferredAction);
        }

        [TestMethod]
        [Description("sucessfully create Episode")]
        public void CreateEpisodeEvent()
        {
            var e1 = FBMessageBuilder.CreateEpisodeEvent("episode-1");
            Assert.AreEqual("episode-1", e1.EpisodeId);
        }
    }
}
