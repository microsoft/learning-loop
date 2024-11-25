// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// using CommonTest;
// using Microsoft.Azure.EventHubs;
// using Microsoft.Azure.Management.Eventhub.Fluent;
// using Microsoft.Azure.Management.Fluent;
// using Microsoft.DecisionService.Common;
// using Microsoft.DecisionService.Common.ARM;
// using Microsoft.DecisionService.Common.Data;
// using Microsoft.DecisionService.Common.Trainer;
// using Microsoft.DecisionService.Common.Trainer.Data;
// using Microsoft.DecisionService.Common.Trainer.Join;
// using Microsoft.DecisionService.Common.Trainer.RewardFunctions;
// using Microsoft.DecisionService.OnlineTrainer;
// using Microsoft.DecisionService.OnlineTrainer.Data;
// using Microsoft.DecisionService.OnlineTrainer.Join;
// using Microsoft.VisualStudio.TestTools.UnitTesting;
// using System;
// using System.Diagnostics;
// using System.Globalization;
// using System.Linq;
// using System.Text;
// using System.Threading;
// using System.Threading.Tasks;
// using Microsoft.Extensions.Logging.Abstractions;
// using EventPosition = Microsoft.DecisionService.Common.Trainer.Join.EventPosition;
// using TestConfiguration = CommonTest.TestConfiguration;
//
// namespace Tests.Join
// {
//     [TestClass]
//     [TestCategory("integ")]
//     [DoNotParallelize]
//     public class JoinerEventHubReceiverTest
//     {
//         private static string connectionString;
//
//         private static IAzure azure;
//         private static IEventHubNamespace eventHubNamespace;
//         private static TestContext testContext;
//
//         private string eventHubEntityPath;
//
//         [ClassInitialize]
//         public async static Task ClassInitializeAsync(TestContext ctx)
//         {
//             testContext = ctx;
//
//             string namespaceResourceId = TestConfiguration.Get(testContext, "joinEventHubReceiverTestEHNamespaceResourceId");
//             connectionString = TestConfiguration.Get(ctx, "joinEventHubReceiverTestConnectionString");
//
//             AzureCredentials azureCredentials = TestAzureCredentials.TestAzureCredentialsFromTestContext(testContext);
//             azure = AzureAuthenticationHelper.AuthenticateAzure(azureCredentials);
//
//             eventHubNamespace = azure.EventHubNamespaces.GetById(namespaceResourceId);
//
//             // clenaup leftover event hubs from previous runs
//             await CleanupEventHubNamespaceAsync();
//         }
//
//         [TestInitialize]
//         public async Task TestInitializeAsync()
//         {
//             var stopwatch = Stopwatch.StartNew();
//
//             // make sure it's unique so we can run multiple concurrently
//             eventHubEntityPath = $"{DateTime.UtcNow:yyyyMMddHHmmssffffff}-{nameof(JoinerEventHubReceiverTest)}";
//             await azure.EventHubs.Define(eventHubEntityPath)
//                 .WithExistingNamespace(eventHubNamespace)
//                 .WithPartitionCount(2)
//                 .WithRetentionPeriodInDays(1)
//                 .CreateAsync();
//
//             stopwatch.Stop();
//             Console.WriteLine("EventHub created in " + stopwatch.Elapsed);
//         }
//
//         [TestCleanup]
//         public async Task TestCleanupAsync()
//         {
//             await CleanupEventHubNamespaceAsync();
//         }
//
//         [TestMethod]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task JoinEventHubReceiverTest_PositiveAsync()
//         {
//             var cancellationTokenSource = new CancellationTokenSource();
//             cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(45));
//
//             Func<int, string> body = (i) => { return $"{{\"EventId\":\"{i}\",\"v\":{i}.2}}"; };
//             var eventsFound = await CreateAndSendEventsAsync(10, body);
//             var targetBlock = new PartitionSource<ObservationEvent>("1", new JoinerOptions());
//
//             var receivers = await this.CreateObservationReceiverAsync(cancellationTokenSource.Token);
//
//             var receiverTasks = receivers
//                 .Select(r => Task.Run(() => r.ForwardAsync(targetBlock)))
//                 .ToList();
//
//             var buffer = targetBlock.Buffer;
//
//             while (eventsFound.Any(x => !x) && !cancellationTokenSource.IsCancellationRequested)
//             {
//                 var batch = buffer.Take(cancellationTokenSource.Token);
//
//                 foreach (var observation in batch)
//                 {
//                     Assert.IsTrue(observation.DataSegment.Count > 1);
//
//                     // validate terminating \0
//                     Assert.AreEqual(0, observation.DataSegment[observation.DataSegment.Count - 1]);
//
//                     var v = int.Parse(observation.EventId, CultureInfo.InvariantCulture);
//
//                     Assert.IsFalse(eventsFound[v], $"Event {v} is duplicated");
//                     eventsFound[v] = true;
//
//                     Assert.AreEqual(v + 0.2f, observation.Value);
//                 }
//             }
//
//             Assert.IsTrue(eventsFound.All(x => x), "Missing events");
//
//             // stop receivers
//             cancellationTokenSource.Cancel();
//
//             await Task.WhenAll(receiverTasks);
//
//             Assert.IsTrue(buffer.IsAddingCompleted);
//             Assert.IsTrue(buffer.IsCompleted);
//         }
//
//         [TestMethod]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task JoinEventHubReceiverTest_PositiveBatchAsync()
//         {
//             var cancellationTokenSource = new CancellationTokenSource();
//             cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(45));
//
//             Func<int, string> body = (i) => { return $"{{\"EventId\":\"{i}\",\"v\":0.0}}\r\n{{\"EventId\":\"{i + 1}\",\"v\":1.0}}"; };
//             var eventsFound = await CreateAndSendEventsAsync(10, body, 2);
//             var targetBlock = new PartitionSource<ObservationEvent>("1", new JoinerOptions());
//
//             var receivers = await this.CreateObservationReceiverAsync(cancellationTokenSource.Token);
//
//             var receiverTasks = receivers
//                 .Select(r => Task.Run(() => r.ForwardAsync(targetBlock)))
//                 .ToList();
//
//             var buffer = targetBlock.Buffer;
//
//             while (eventsFound.Any(x => !x) && !cancellationTokenSource.IsCancellationRequested)
//             {
//                 var batch = buffer.Take(cancellationTokenSource.Token);
//
//                 foreach (var observation in batch)
//                 {
//                     Assert.IsTrue(observation.DataSegment.Count > 1);
//
//                     // validate terminating \0
//                     Assert.AreEqual(0, observation.DataSegment[observation.DataSegment.Count - 1]);
//
//                     var v = int.Parse(observation.EventId, CultureInfo.InvariantCulture);
//
//                     Assert.IsFalse(eventsFound[v], $"Event {v} is duplicated");
//                     eventsFound[v] = true;
//
//                     Assert.AreEqual((float)(v % 2), observation.Value);
//                 }
//             }
//
//             Assert.IsTrue(eventsFound.All(x => x), "Missing events");
//
//             // stop receivers
//             cancellationTokenSource.Cancel();
//
//             await Task.WhenAll(receiverTasks);
//
//             Assert.IsTrue(buffer.IsAddingCompleted);
//             Assert.IsTrue(buffer.IsCompleted);
//         }
//
//         [TestMethod]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task JoinEventHubReceiverTest_HandlesInvalidOffsetAsync()
//         {
//             var cancellationTokenSource = new CancellationTokenSource();
//             cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(45));
//
//             //Create event position with invalid offset
//             EventPosition start = new EventPosition { Offset = long.MaxValue, EnqueuedTimeUtc = DateTime.UtcNow };
//
//             var eventsFound = await CreateAndSendEventsAsync(1);
//             var targetBlock = new PartitionSource<InteractionEvent>("0", new JoinerOptions());
//             var receivers = await this.CreateInteractionReceiverAsync(cancellationTokenSource.Token, start);
//
//             var receiverTasks = receivers
//                 .Select(r => Task.Run(() => r.ForwardAsync(targetBlock)))
//                 .ToList();
//
//             var buffer = targetBlock.Buffer;
//
//             try
//             {
//                 while (eventsFound.Any(x => !x) && !cancellationTokenSource.IsCancellationRequested)
//                 {
//                     var batch = buffer.Take(cancellationTokenSource.Token);
//
//                     foreach (var interaction in batch)
//                     {
//                         var v = int.Parse(interaction.EventId, CultureInfo.InvariantCulture);
//                         eventsFound[v] = true;
//                     }
//                 }
//             }
//             catch (OperationCanceledException)
//             {
//                 Assert.IsTrue(cancellationTokenSource.IsCancellationRequested);
//             }
//
//             Assert.IsTrue(eventsFound.All(x => x), "Missing events");
//
//             // stop receivers
//             cancellationTokenSource.Cancel();
//
//             await Task.WhenAll(receiverTasks);
//
//             Assert.IsTrue(buffer.IsAddingCompleted);
//             Assert.IsTrue(buffer.IsCompleted);
//         }
//
//
//         public async Task<bool[]> CreateAndSendEventsAsync(int numEvents, Func<int, string> generateEventBody = null, int step = 1)
//         {
//             if (generateEventBody == null)
//             {
//                 generateEventBody = (i) => { return $"{{\"EventId\":\"{i}\"}}"; };
//             }
//             var eventsFound = new bool[numEvents]; // default to false
//
//             var client = this.CreateEventHubClient();
//             for (int i = 0; i < numEvents; i += step)
//             {
//                 await client.SendAsync(new EventData(Encoding.UTF8.GetBytes(generateEventBody(i))), (i % 2).ToString());
//             }
//
//             return eventsFound;
//         }
//
//         private EventHubClient CreateEventHubClient()
//         {
//             var builder = new EventHubsConnectionStringBuilder(connectionString) { EntityPath = eventHubEntityPath };
//
//             return EventHubClient.CreateFromConnectionString(builder.ToString());
//         }
//
//         private static async Task CleanupEventHubNamespaceAsync()
//         {
//             var eventHubs = await eventHubNamespace.ListEventHubsAsync();
//             foreach (var eh in eventHubs)
//             {
//                 await azure.EventHubs.DeleteByIdAsync(eh.Id);
//             }
//         }
//
//         private Task<JoinerEventHubReceiverInteraction[]> CreateInteractionReceiverAsync(CancellationToken cancellationToken, EventPosition lastOffset = null)
//         {
//             var joinerOptions = new JoinerOptions
//             {
//                 CancellationToken = cancellationToken,
//                 TimeProvider = SystemTimeProvider.Instance,
//                 Options = new OnlineTrainerOptions
//                 {
//                     Logger = NullLogger.Instance,
//                     EventHubConnectionString = connectionString,
//                 },
//             };
//
//             return JoinerEventHub.CreateAsync(
//                 joinerOptions,
//                 eventHubEntityPath,
//                 typeof(InteractionEvent).Name,
//                 new EventHubReceiverClientFactory(),
//                 (eventHubReceiverClient, eventHubsenderClient, partitionId) => new JoinerEventHubReceiverInteraction(
//                     eventHubReceiverClient,
//                     partitionId,
//                     lastOffset,
//                     joinerOptions,
//                     eventHubsenderClient
//                     ));
//         }
//
//         private Task<JoinerEventHubReceiverObservation[]> CreateObservationReceiverAsync(
//             CancellationToken cancellationToken,
//             EventPosition lastOffset = null)
//         {
//             var joinerOptions = new JoinerOptions
//             {
//                 CancellationToken = cancellationToken,
//                 TimeProvider = SystemTimeProvider.Instance,
//                 Options = new OnlineTrainerOptions
//                 {
//                     Logger = NullLogger.Instance,
//                     RewardFunction = RewardFunction.earliest,
//                     EventHubConnectionString = connectionString
//                 }
//             };
//
//             return JoinerEventHub.CreateAsync(
//                 joinerOptions,
//                 eventHubEntityPath,
//                 typeof(ObservationEvent).Name,
//                 new EventHubReceiverClientFactory(),
//                 (eventHubReceiverClient, eventHubsenderClient, partitionId) => new JoinerEventHubReceiverObservation(
//                     eventHubReceiverClient,
//                     partitionId,
//                     lastOffset,
//                     joinerOptions,
//                     eventHubsenderClient));
//         }
//     }
// }
