// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Tests.E2ETools
{
    public interface IFileFetcher
    {
        int ExpectedNumRequests { get; set; }
        public DateTime ExpectedBlobTimestamp { get; set; }
        DateTime LastConfEditDate { get; set; }
        string CookedLogName { get; set; }
        string SkippedLogName { get; set; }
        string CurrentModelName { get; set; }

        List<CookedLogLine> GetCookedLogs(string logSource, int? logHour = null);
        List<CookedLogLine> GetSkippedLogs(string logSource, int? logHour = null);

        /// <summary>
        /// Downloads Model file to a local file and returns path to the file
        /// </summary>
        /// <returns></returns>
        string DownloadModelFile(bool clientModel = true);

        string TryDownloadCookedLogs(string logSource, int? logHour = null);
        string TryDownloadSkippedLogs(string logSource, int? logHour = null);
    }
}