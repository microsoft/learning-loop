// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Instrumentation;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Microsoft.DecisionService.OnlineTrainer
{
    public sealed class StorageUploadMetrics
    {
        private readonly KeyValuePair<string, object> _appIdProperty;
        
        private readonly Counter<long> _learnableUploadBytes;
        private readonly Counter<long> _learnableUploadExamples;
        
        private readonly Counter<long> _skippedUploadBytes;
        private readonly Counter<long> _skippedUploadExamples;
        

        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="storageUploadKind">Mirror or normal</param>
        /// <param name="meterFactory"></param>
        public StorageUploadMetrics(string appId, string storageUploadKind, IMeterFactory meterFactory)
        {
            _appIdProperty = new KeyValuePair<string, object?>(MetricsUtil.AppIdKey, appId);

            var meter = meterFactory?.Create("Microsoft.DecisionService.OnlineTrainer.StorageUploadMetrics", "1.0");
            
            _learnableUploadBytes = meter?.CreateCounter<long>($"StorageUpload.{storageUploadKind}.Learnable.Bytes");
            _learnableUploadExamples = meter?.CreateCounter<long>($"StorageUpload.{storageUploadKind}.Learnable.Examples");
            
            _skippedUploadBytes = meter?.CreateCounter<long>($"StorageUpload.{storageUploadKind}.Skipped.Bytes");
            _skippedUploadExamples = meter?.CreateCounter<long>($"StorageUpload.{storageUploadKind}.Skipped.Examples");
            
        }
        
        public void UploadedLearnableBytes(long bytes)
        {
            _learnableUploadBytes?.Add(bytes, _appIdProperty);
        }
        
        public void UploadedLearnableExamples(long examples)
        {
            _learnableUploadExamples?.Add(examples, _appIdProperty);
        }
        
        public void SkippedBytes(long bytes)
        {
            _skippedUploadBytes?.Add(bytes, _appIdProperty);
        }
        
        public void SkippedExamples(long examples)
        {
            _skippedUploadExamples?.Add(examples, _appIdProperty);
        }
        
        
        
    }
}