// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Castle.DynamicProxy.Generators.Emitters.SimpleAST;
using CommonTest.Messages;
using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Storage;
using Microsoft.DecisionService.Common.Trainer.Data;
using Microsoft.DecisionService.OnlineTrainer;
using Newtonsoft.Json;
using reinforcement_learning.messages.flatbuff.v2;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace CommonTest
{
    public static class TestStorageHelper
    {
        public static string GetTestFileBasePath(params string[] relativeTestPath)
        {
            return Path.Join(AppDomain.CurrentDomain.BaseDirectory, Path.Combine(relativeTestPath));
        }

        private static int FindStartingPositionOf(byte[] pattern, byte[] data)
        {
            int position = -1;
            for (int i = 0; i <= data.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                {
                    position = i;
                    break;
                }
            }
            return position;
        }

        public static MemoryStream LoadFromFile(string path)
        {
            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            var memoryStream = new MemoryStream();
            fileStream.CopyTo(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);
            return memoryStream;
        }

        public static async Task UploadCheckpointFileToBlobAsync(
            IBlobContainerClient container,
            string checkpointFilename,
            string destBlobName,
            CancellationToken cancellationToken = default)
        {
            // NOTE: we are cheating here by assuming the JSON payload is at the end of the file
            //       and the model is at the beginning. this is not a general solution and may
            //       break based on this assumption.
            using var payload = LoadFromFile(checkpointFilename);
            var pattern = Encoding.UTF8.GetBytes("{\"Timestamp\":");
            var payloadBuf = payload.GetBuffer();
            int position = FindStartingPositionOf(pattern, payloadBuf);
            if (position == -1)
            {
                throw new FileLoadException($"Invalid checkpoint file {checkpointFilename}: cannot find metadata block");
            }
            using var modelBlock = new MemoryStream(payloadBuf, 0, position);
            using var metaBlock = new MemoryStream(payloadBuf, position, payloadBuf.Length - position);
            var checkpointBlob = container.GetBlockBlobClient(destBlobName);
            var modelBlockId = CheckpointBlockHelper.GetModelBlockName(0);
            await checkpointBlob.WriteBlockAsync(modelBlockId, modelBlock, cancellationToken);
            var metaBlockId = CheckpointBlockHelper.GetMetadataBlockName(0);
            await checkpointBlob.WriteBlockAsync(metaBlockId, metaBlock, cancellationToken);
            await checkpointBlob.CommitBlocksAsync(new[] { modelBlockId, metaBlockId }, cancellationToken);
        }

        public static async Task UploadFileToBlobAsync(
            IBlobContainerClient container,
            string sourceFilename,
            string destBlobName,
            CancellationToken cancellationToken = default)
        {
            // NOTE: each file type may need additional logic to handle loading blocks correctly
            //       so far, all we need is the checkpoint file handling for testing purposes
            //       more may be required in the future.
            using var payload = TestStorageHelper.LoadFromFile(sourceFilename);
            if (Path.GetFileName(sourceFilename) == AzureBlobConstants.CheckpointBlobName)
            {
                await UploadCheckpointFileToBlobAsync(container, sourceFilename, destBlobName, cancellationToken);
            }
            else
            {
                await container.GetBlockBlobClient(destBlobName).WriteAsync(payload, cancellationToken);
            }
        }

        public static async Task UploadFilesToBlobAsync(string testDirPath, IBlobContainerClient container, CancellationToken cancellationToken = default)
        {
            var sourceFilenames = Directory.GetFiles(testDirPath, "*", SearchOption.AllDirectories);
            // note: don't do this in parralel, it will cause threading issues for MemBlobContainerClient
            foreach (var filename in sourceFilenames)
            {
                var blobName = filename[(testDirPath.Length + 1)..];
                blobName = Regex.Replace(blobName, @"\\", @"/");
                await UploadFileToBlobAsync(container, filename, blobName, cancellationToken);
            }
        }

        public static DateTime ExtractCheckpointTimestamp(string testDirPath)
        {
            var checkpointFiles = Directory.GetFiles(testDirPath, "storage-checkpoint.json", SearchOption.AllDirectories);
            foreach (var checkpointFile in checkpointFiles)
            {
                var checkpointContent = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(checkpointFile));
                if (!checkpointContent.TryGetProperty("BlobProperty", out JsonElement blobProperty)) {
                    continue;
                }
                if (!blobProperty.TryGetProperty("BlobName", out JsonElement blobName)) {
                    continue;
                }
                var name = blobName.GetString();
                var matches = Regex.Match(name, @"(\d{4})(\d{2})(\d{2})");
                if (!matches.Success)
                {
                    continue;
                }
                int year = int.Parse(matches.Groups[1].Value);
                int month = int.Parse(matches.Groups[2].Value);
                int day = int.Parse(matches.Groups[3].Value);
                return new DateTime(year, month, day);
            }
            return DateTime.MinValue;
        }

        public static StorageCheckpoint CreateStorageCheckpoint(BlockPosition position, EventHubCheckpoint eventsCheckpoint, BlobProperty blobProperty)
        {
            return new StorageCheckpoint()
            {
                BlockPosition = position,
                EventPosition = eventsCheckpoint,
                BlobProperty = blobProperty
            };
        }

        public static StorageCheckpoint CreateStorageCheckpoint(BlockPosition position, IList<PartitionCheckpoint> partitionCheckpoints, BlobProperty blobProperty)
        {
            EventHubCheckpoint eventsCheckpoint = null;
            if (partitionCheckpoints != null)
            {
                eventsCheckpoint = new EventHubCheckpoint();
                for (int i = 0; i < partitionCheckpoints.Count; i++)
                {
                    eventsCheckpoint.PartitionCheckpoints.Add(
                        i.ToString(),
                        new PartitionCheckpoint() { Offset = partitionCheckpoints[i].Offset, EnqueuedTimeUtc = partitionCheckpoints[i].EnqueuedTimeUtc }
                    );
                }
            }
            return CreateStorageCheckpoint(position, eventsCheckpoint, blobProperty);
        }

        public static StorageCheckpoint CreateStorageCheckpoint(string blobName, int blockNo = 0, int blobLength = 0, IList<PartitionCheckpoint> partitionCheckpoints = null)
        {
            var position = new BlockPosition()
            {
                BlobName = blobName,
                BlockName = blockNo.ToString("x4")
            };
            var blobProp = new BlobProperty()
            {
                BlobName = blobName,
                Length = blobLength
            };
            return CreateStorageCheckpoint(position, partitionCheckpoints, blobProp);
        }

        public static async Task UploadCheckpointAsync(IBlobClient storageCheckpointBlob, StorageCheckpoint checkpoint)
        {
            string serializedCP = JsonConvert.SerializeObject(checkpoint);
            await storageCheckpointBlob.UploadAsync(BinaryData.FromString(serializedCP));
        }

        private static async Task<StorageCheckpoint> CreateAndUploadCheckpointAsync(
            IBlobClient storageCheckpointBlob,
            BlockPosition blockPosition,
            long eventPositionOffset)
        {
            var cp = CreateStorageCheckpoint(
                blockPosition,
                new List<PartitionCheckpoint>() { new() { Offset = eventPositionOffset } },
                null
            );
            await UploadCheckpointAsync(storageCheckpointBlob, cp);
            return cp;
        }

        private static async Task<StorageCheckpoint> CreateAndUploadCheckpointAsync(
            IBlobClient storageCheckpointBlob,
            IBlockStore blob,
            string blockName,
            long eventPositionOffset,
            JoinedLogFormat format = JoinedLogFormat.Binary)
        {
            return await CreateAndUploadCheckpointAsync(
                storageCheckpointBlob,
                new BlockPosition()
                {
                    BlobName = blob.Name,
                    BlockName = blockName,
                    FileFormat = format
                },
                eventPositionOffset
            );
        }


        public static IBlockStore CreateDataBlobReference(IBlobContainerClient container, DateTime lastConfigEditDate, DateTime blobDate, int blobIndex, JoinedLogFormat format = JoinedLogFormat.Binary, int? blobHour = null)
        {
            string blobName = PathHelper.BuildBlobName(lastConfigEditDate, blobDate, blobIndex, format: format, blobHour: blobHour);
            return container.GetBlockBlobClient(blobName);
        }

        private static async Task UploadBlockAsync(IBlockStore blob, string blockName, int eventCount = 1, bool newBlob = true)
        {
            using var blockStream = new MemoryStream();
            var events = global::CommonTest.TestUtil.GenerateEvents(eventCount, "testappid", "modelid");
            var eventBatch = FBMessageBuilder.CreateEventBatch(events);
            blockStream.Write(eventBatch.ByteBuffer.ToSizedArray());
            blockStream.Position = 0;
            await blob.WriteBlockAsync(blockName, blockStream, CancellationToken.None);
        }

        private static async Task CommitBlocksAsync(IBlockStore blob, int dataBlockCount, bool appendOffsetBlock = false)
        {
            IList<string> ids = Enumerable.Range(1, dataBlockCount).Select(id => id.ToString("x4")).ToList();
            if (appendOffsetBlock)
            {
                ids.Add("FFFF");
            }
            await blob.CommitBlocksAsync(ids);
        }

        public static async Task<int> GenerateAndSendEventsListToUploadBlockAsync(StorageUploadBlock uploadBlock, DateTime[] eventBatchStartTimes)
        {
            int batchCount = 0;
            foreach (DateTime eventBatchStartTime in eventBatchStartTimes)
            {
                using var blockStream = new MemoryStream();
                var events = global::CommonTest.TestUtil.GenerateEvents(1, "testappid", "modelid");
                var eventBatch = FBMessageBuilder.CreateEventBatch(events);
                blockStream.Write(eventBatch.ByteBuffer.ToSizedArray());

                var serializedBatch = new SerializedBatch()
                {
                    EnqueuedTimeUtc = eventBatchStartTime,
                    Offset = 1000,
                    SequenceNumber = 0,
                    PartitionId = "0",
                    SourceMessageEventCount = 1,
                    payload = new ArraySegment<byte>(blockStream.ToArray())
                };

                await uploadBlock.Input.SendAsync(new List<SerializedBatch> { serializedBatch });
                batchCount++;
            }
            return batchCount;
        }

        public static async Task<BlockPosition?> PrepStorageForDownloadAsync(
            IBlobContainerClient blobContainerClient,
            IBlobClient storageCheckpointBlob,
            DateTime lastConfigUpdate,
            int blobIndex,
            DateTime blobDate,
            int uploadBlockCount,
            int commitBlockCount,
            bool useHourlyIncrement = false,
            JoinedLogFormat format = JoinedLogFormat.Binary,
            int eventCount = 1,
            int startAtCheckpointNo = -1 /* last */)
        {
            // note: the checkpoint localation will be at the last staged block (uploadBlockCount)
            int startAt = startAtCheckpointNo < 0 ? uploadBlockCount : startAtCheckpointNo;
            int? blobHour = useHourlyIncrement ? blobDate.Hour : null;
            var blob = CreateDataBlobReference(blobContainerClient, lastConfigUpdate, blobDate, blobIndex, blobHour: blobHour);
            await UploadAndCommitBlocksToBlobAsync(blob, uploadBlockCount, commitBlockCount, eventCount);
            if (startAt >= 0)
            {
                var chekpoint = await CreateAndUploadCheckpointAsync(
                    storageCheckpointBlob,
                    blob,
                    blockName: startAt.ToString("x4"),
                    eventPositionOffset: 0
                );
                return chekpoint.BlockPosition;
            }
            return new BlockPosition()
            {
                BlobName = PathHelper.BuildBlobName(lastConfigUpdate, blobDate, blobIndex, format: format),
                FileFormat = format
            };
        }

        /// <summary>
        /// Upload and commit blocks to blob.
        /// </summary>
        /// <param name="blob">the blob</param>
        /// <param name="uploadBlockCount">the number of block to stage</param>
        /// <param name="commitBlockCount">the number of staged blocks to commit</param>
        /// <param name="eventCount">the number of events to generate for each block</param>
        /// <remarks>
        /// uploadBlockCount should be greater than or equal to commitBlockCount; if not,
        /// the remaining blocks will be discarded.  This method is used to simulate when
        /// blocks are staged, but trailing blocks are not committed (specified by uploadBlockCount - commitBlockCount).
        /// </remarks>
        private static async Task UploadAndCommitBlocksToBlobAsync(IBlockStore blob, int uploadBlockCount, int commitBlockCount, int eventCount = 1)
        {
            for (int i = 1; i <= uploadBlockCount; i++)
            {
                await UploadBlockAsync(blob, i.ToString("x4"), eventCount: eventCount);
                if (i <= commitBlockCount)
                {
                    // keep this in the loop, otherwise blocks not committed are discarded (we want them to be staged -- uncommitted)
                    await CommitBlocksAsync(blob, i);
                }
            }
        }

        public static async Task<(int, int, Exception)> ReadDownloadDataBlocksAsync(ISourceBlock<BlockData> source, TimeSpan timeout)
        {
            int receiveAttempts = 1;
            int numDownloadedBlocks = 0;
            Exception exception = null;
            // We should be receiving data for the first calls and hit exception when no data available, having 10 here just to be protective.
            try
            {
                for (; receiveAttempts < 10; receiveAttempts++)
                {
                    var dataReceived = await source.ReceiveAsync(timeout);
                    numDownloadedBlocks++;
                }
            }
            catch (Exception e)
            {
                exception = e;
            }
            return (receiveAttempts, numDownloadedBlocks, exception);
        }

        public static async Task<(int, int, Exception)> ReadDownloadEventsAsync(ISourceBlock<BlockData> source, TimeSpan timeout)
        {
            int receiveAttempts = 1;
            int numDownloadedEvents = 0;
            Exception exception = null;
            try
            {
                // We should be receiving data for the first calls and hit exception when no data available, having 10 here just to be protective.
                for (; receiveAttempts < 10; receiveAttempts++)
                {
                    var dataReceived = await source.ReceiveAsync(timeout);
                    var events = EventBatch.GetRootAsEventBatch(new Google.FlatBuffers.ByteBuffer(dataReceived.Data));
                    numDownloadedEvents += events.EventsLength;
                }
            }
            catch (Exception e) // System.Exception for timeout expected on last attempt to call ReceiveAsync, have to check error message to verify.
            {
                exception = e;
            }
            return (receiveAttempts, numDownloadedEvents, exception);
        }
    }
}
