// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Identity;
using CommonTest.Fakes.Storage.InMemory;
using CommonTest.Messages;
using Microsoft.DecisionService.Common.Storage;
using Microsoft.DecisionService.Common.Storage.Azure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using reinforcement_learning.messages.flatbuff.v2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonTest
{
    public static class TestUtil
    {
        /// <summary>
        /// Creates an instance of IStorageFactory based on the provided configuration.
        /// </summary>
        /// <param name="context">Provides context for the test, including configuration settings.</param>
        /// <param name="configStoreKey">A key to retrieve the storage URI from the configuration. Defaults to Constants.e2eStorageUriKey.</param>
        /// <returns>An instance of IStorageFactory which can be either MemStorageFactory or AzStorageFactory based on the configuration.</returns>
        public static IStorageFactory CreateStorageFactory(TestContext context, string configStoreKey = Constants.e2eStorageUriKey)
        {
            var storageUriStr = TestConfiguration.TryGet(context, configStoreKey) ?? Constants.memStoreUri;
            if (storageUriStr.StartsWith(Constants.memStoreUri))
            {
                return new MemStorageFactory(new Uri(storageUriStr));
            }
            else
            {
                var managedClientId = TestConfiguration.TryGet(context, Constants.managedIdentityClientId);
                if (string.IsNullOrEmpty(managedClientId))
                {
                    return new AzStorageFactory(new Uri(storageUriStr), new DefaultAzureCredential());
                }
                else
                {
                    return new AzStorageFactory(new Uri(storageUriStr), new ManagedIdentityCredential(managedClientId));
                }
            }
        }

        /// <summary>
        /// Generate a list of events.
        /// </summary>
        /// <param name="count">Total count of interaction events.</param>
        /// <param name="appId">Application id.</param>
        /// <param name="modelId">Model id.</param>
        /// <param name="deferredAction">DeferredAction will bet set.</param>
        /// <param name="startDateTime">EnqueuedTimeUtc of the event.</param>
        /// <returns></returns>
        public static List<Event> GenerateEvents(
            int count,
            string appId,
            string modelId,
            bool deferredAction = false,
            DateTime? startDateTime = null)
        {
            if (startDateTime == null)
            {
                startDateTime = DateTime.UtcNow;
            }
            var startDt = startDateTime.Value;

            return Enumerable.Range(0, count).Select(i => FBMessageBuilder.CreateEvent(
                appId,
                $"event-id-{i}",
                startDt.AddSeconds(i),
                EventEncoding.Identity,
                0.0f,
                FBMessageBuilder.CreateCbEvent(
                    deferredAction,
                    new ulong[] { 1, 2, 3 },
                    Encoding.UTF8.GetBytes("test-context-data"),
                    new float[] { 0.2f, 0.3f, 0.4f },
                    modelId,
                    LearningModeType.Online)
                )
            ).ToList();
        }

        // Assert that the provided function becomes true within the next 30 seconds.
        public static async Task AssertEventuallyTrueAsync(Func<bool> test)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(30);
            await AssertEventuallyTrueAsync(test, timeSpan);
        }

        // Assert that the provided function becomes true within the provided timespan.
        public static async Task AssertEventuallyTrueAsync(Func<bool> test, TimeSpan timeSpan)
        {
            var deadline = DateTime.Now + timeSpan;
            while (DateTime.Now < deadline)
            {
                if (test.Invoke())
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10));
            }

            Assert.Fail($"test function did not become true within {timeSpan}");
        }
        

        // Assert that the provided function becomes true within the provided timespan.
        public static async Task AssertEventuallyTrueAsync(Func<Task<bool>> testFactory, TimeSpan timeSpan)
        {
            var deadline = DateTime.Now + timeSpan;
            while (DateTime.Now < deadline)
            {
                var testFunction = testFactory.Invoke();
                if (await testFunction)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10));
            }

            Assert.Fail($"test function did not become true within {timeSpan}");
        }

        // Assert that the provided function runs without throwing an exception within the provided timespan.
        public static async Task AssertEventuallyNoThrowAsync(Func<Task> testFactory, TimeSpan timeSpan)
        {
            var deadline = DateTime.Now + timeSpan;
            Exception capturedException = null;
            while (DateTime.Now < deadline)
            {
                try
                {
                    var test = testFactory.Invoke();
                    await test;
                    return;
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                }
            }

            Assert.Fail($"test function still throwing exception after {timeSpan}: {capturedException}");
        }

        // Assert that the provided function is true and remains true for the next 100 milliseonds.
        public static async Task AssertStaysTrueAsync(Func<bool> test)
        {
            TimeSpan timeSpan = TimeSpan.FromMilliseconds(100);
            await AssertStaysTrueAsync(test, timeSpan);
        }

        // Assert that the provided function is true and remains true for the provided timespan.
        public static async Task AssertStaysTrueAsync(Func<bool> test, TimeSpan timeSpan)
        {
            var deadline = DateTime.Now + timeSpan;
            while (DateTime.Now < deadline)
            {
                if (!test.Invoke())
                {
                    Assert.Fail($"test function became false after {deadline - DateTime.Now}");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10));
            }
        }
    }
}
