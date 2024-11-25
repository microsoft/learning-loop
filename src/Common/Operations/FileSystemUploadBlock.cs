// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// using Microsoft.DecisionService.Common.Data;
// using Microsoft.DecisionService.OnlineTrainer.Data;
// using Microsoft.DecisionService.OnlineTrainer.Operations;
// using System;
// using System.IO;
// using System.Threading;
// using System.Threading.Tasks;
// using System.Threading.Tasks.Dataflow;
//
// namespace Microsoft.DecisionService.Common.Trainer.Operations
// {
//     public class FileSystemUploadBlock : ITargetBlock<InteractionEvent>
//     {
//         private const int BufferCapacity = 10;
//         private readonly StorageBlockOptions options;
//         private readonly string path;
//
//         private readonly ActionBlock<InteractionEvent> internalBlock;
//         private readonly UploadHelper uploadHelper;
//
//         private string curFilepath = null;
//         private BinaryWriter curWriter = null;
//
//
//         public Task Completion => internalBlock.Completion;
//
//         public FileSystemUploadBlock(StorageBlockOptions options, string path, UploadHelper uploadHelper, CancellationToken cancellationToken)
//         {
//             this.options = options;
//             this.path = path;
//             this.uploadHelper = uploadHelper;
//
//             internalBlock = new ActionBlock<InteractionEvent>(evt => this.ProcessInteractionEventAsync(evt), new ExecutionDataflowBlockOptions()
//             {
//                 CancellationToken = cancellationToken
//             });
//
//             internalBlock.Completion.ContinueWith(task => this.CloseWriter(), TaskScheduler.Default);
//         }
//
//         public void Complete()
//         {
//             internalBlock.Complete();
//         }
//
//         public void Fault(Exception exception)
//         {
//             (internalBlock as ITargetBlock<InteractionEvent>).Fault(exception);
//         }
//
//         public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, InteractionEvent messageValue, ISourceBlock<InteractionEvent> source, bool consumeToAccept)
//         {
//             return (internalBlock as ITargetBlock<InteractionEvent>).OfferMessage(messageHeader, messageValue, source, consumeToAccept);
//         }
//
//         private static bool IsEventLearnable(InteractionEvent e) => !e.IsDanglingObservation && !e.SkipLearn;
//
//         private void CloseWriter()
//         {
//             if (curWriter != null)
//             {
//                 curWriter.Close();
//                 curWriter = null;
//             }
//         }
//
//         private Task ProcessInteractionEventAsync(InteractionEvent evt)
//         {
//             // Only upload skipped events if option is enabled.
//             bool shouldUploadEvent = IsEventLearnable(evt) || this.options.UploadSkippedLogs;
//             if (!shouldUploadEvent)
//             {
//                 return Task.CompletedTask;
//             }
//
//             string subPath = IsEventLearnable(evt) ? AzureBlobConstants.CookedLogsDirectoryPrefix : AzureBlobConstants.SkippedLogsDirectoryPrefix;
//             // Local instances to limit the number of file open operations.
//             JoinedLogFormat format = evt.IsOpaqueEvent ? JoinedLogFormat.Binary : JoinedLogFormat.DSJSON;
//             string blobPath = Path.Combine(path, PathHelper.BuildBlobName(this.options.LastConfigurationEditDate, evt.EnqueuedTimeUtc, 0, subPath, format));
//
//             if (blobPath != curFilepath)
//             {
//                 CloseWriter();
//
//                 curFilepath = blobPath;
//                 Directory.CreateDirectory(Path.GetDirectoryName(curFilepath));
//
//                 curWriter = new BinaryWriter(new FileStream(curFilepath, FileMode.Append, FileAccess.Write, FileShare.Read));
//             }
//
//             // Write the event to file
//             if (curWriter != null)
//             {
//                 evt = uploadHelper.Serialize(evt);
//                 curWriter.Write(evt.DataSegment);
//                 curWriter.Flush();
//             }
//
//             return Task.CompletedTask;
//         }
//     }
// }
