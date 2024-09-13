// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Tests.E2ETools
{
    abstract class BaseFileFetcher : IFileFetcher
    {
        private readonly ILogParser logParser;
        protected readonly TestContext testContext;

        public BaseFileFetcher(ILogParser logParser, TestContext testContext)
        {
            this.logParser = logParser;
            this.testContext = testContext;
        }

        public int ExpectedNumRequests { get; set; }
        public DateTime ExpectedBlobTimestamp { get; set; } = DateTime.UtcNow;
        public DateTime LastConfEditDate { get; set; }
        // File name of the cooked log
        public string CookedLogName { get; set; }
        public string SkippedLogName { get; set; }
        // File name of the original model
        public string CurrentModelName { get; set; }

        /// <summary>
        /// Download cooked logs, retrying until the expected number of requests are found
        /// </summary>
        public List<CookedLogLine> GetCookedLogs(string logSource, int? blobHour)
        {
            return GetLogs(this.TryDownloadCookedLogs, logSource, blobHour);
        }

        /// <summary>
        /// Download skipped logs, retrying until the expected number of requests are found
        /// </summary>
        public List<CookedLogLine> GetSkippedLogs(string logSource, int? blobHour)
        {
            return GetLogs(this.TryDownloadSkippedLogs, logSource, blobHour);
        }

        public abstract string TryDownloadCookedLogs(string logSource, int? blobHour);

        public abstract string TryDownloadSkippedLogs(string logSource, int? blobHour);

        public abstract string DownloadModelFile(bool clientModel = true);

        private List<CookedLogLine> GetLogs(Func<string, int?, string> downloader, string logSource, int? blobHour)
        {
            List<CookedLogLine> cookedLogs = new List<CookedLogLine>();
            int retryCount = 50;
            try
            {
                while (cookedLogs.Count < this.ExpectedNumRequests && retryCount-- > 0)
                {
                    string logs = downloader(logSource, blobHour);
                    cookedLogs = this.logParser.ParseLogs(logs);
                    Thread.Sleep(TimeSpan.FromMilliseconds(500));
                }
            }
            catch (Exception e)
            {
                testContext.WriteLine("Error downloading logs: " + e.Message);
            }

            return cookedLogs;
        }
    }
}
