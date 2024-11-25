// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.DecisionService.Common.Data;
using Microsoft.Extensions.Logging;

namespace Microsoft.DecisionService.Common.Trainer.Operations
{
    class FileSystemDownloadBlock : ISourceBlock<BlockData>
    {
        private const int BufferedCapacity = 10;
        private const int MillisecondsTimeout = 1000;
        private readonly string path;
        private readonly BufferBlock<BlockData> bufferBlock;
        private readonly ILogger logger;

        private long curPosition;
        private string curFilename;
        private bool isRunning = true;

        private readonly FileSystemWatcher fsw;
        private readonly object lockObj = new object();
        private Task readerTask;

        public Task Completion { get; }

        public FileSystemDownloadBlock(ModelCheckpoint modelCheckpoint, string path, ILogger logger, CancellationToken cancellationToken)
        {
            this.logger = logger;
            this.bufferBlock = new BufferBlock<BlockData>(new DataflowBlockOptions()
            {
                BoundedCapacity = BufferedCapacity,
                CancellationToken = cancellationToken
            });
            Completion = this.bufferBlock.Completion.ContinueWith(beforeTask => StopReader(), TaskScheduler.Default);

            this.curPosition = 0;
            if (modelCheckpoint?.ReadingPosition != null)
            {
                this.curFilename = modelCheckpoint.ReadingPosition.BlobName;
                long.TryParse(modelCheckpoint.ReadingPosition.BlockName, out this.curPosition);
            }

            this.path = path;

            fsw = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            fsw.Created += OnFileChange;
            fsw.Changed += OnFileChange;

            readerTask = Task.Run(() => RunReaderAsync());
        }


        public void Complete()
        {
            bufferBlock.Complete();
        }

        public BlockData ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<BlockData> target, out bool messageConsumed)
        {
            return (bufferBlock as ISourceBlock<BlockData>).ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public void Fault(Exception exception)
        {
            (bufferBlock as ISourceBlock<BlockData>).Fault(exception);
        }

        public IDisposable LinkTo(ITargetBlock<BlockData> target, DataflowLinkOptions linkOptions)
        {
            return bufferBlock.LinkTo(target, linkOptions);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<BlockData> target)
        {
            (bufferBlock as ISourceBlock<BlockData>).ReleaseReservation(messageHeader, target);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<BlockData> target)
        {
            return (bufferBlock as ISourceBlock<BlockData>).ReserveMessage(messageHeader, target);
        }

        private IEnumerable<string> ListFiles(string path)
        {
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);
        }

        private void OnFileChange(object sender, FileSystemEventArgs args)
        {
            lock (lockObj)
            {
                Monitor.Pulse(lockObj);
            }
        }

        private void StopReader()
        {
            isRunning = false;
            lock (lockObj)
            {
                Monitor.Pulse(lockObj);
            };
            Task.WaitAll(readerTask);
        }

        private async Task RunReaderAsync()
        {
            while (isRunning)
            {
                try
                {
                    foreach (string filepath in ListFiles(this.path))
                    {
                        if (filepath.CompareTo(curFilename) >= 0)
                        {
                            if (filepath != curFilename)
                            {
                                curPosition = 0;
                            }

                            curFilename = filepath;

                            foreach (BlockData bytes in ReadEvents(curFilename, curPosition))
                            {
                                if (!isRunning)
                                {
                                    return;
                                }

                                if (await bufferBlock.SendAsync(bytes))
                                {
                                    curPosition += bytes.Data.Length;
                                }
                            }
                        }
                    }

                    lock (lockObj)
                    {
                        Monitor.Wait(lockObj, MillisecondsTimeout);
                    }
                }
                catch (InvalidOperationException e)
                {
                    this.logger.LogError(e, "");
                }
            }
        }

        private IEnumerable<BlockData> ReadEvents(string filepath, long position)
        {
            const long MaxBuffer = 1024 * 1024;

            using (var reader = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long curPosition = position;
                reader.Position = curPosition;

                long bufferSize = Math.Min(MaxBuffer, reader.Length - reader.Position);
                byte[] bytes = new byte[bufferSize];

                while (reader.Read(bytes, 0, bytes.Length) > 0)
                {
                    yield return new BlockData
                    {
                        Data = bytes,
                        Position = new BlockPosition
                        {
                            BlobName = filepath,
                            BlockName = curPosition.ToString()
                        }
                    };

                    curPosition += bufferSize;
                    reader.Position = curPosition;

                    // Create new buffer for new events
                    bufferSize = Math.Min(MaxBuffer, reader.Length - reader.Position);
                    bytes = new byte[bufferSize];
                }
            }
        }
    }
}
