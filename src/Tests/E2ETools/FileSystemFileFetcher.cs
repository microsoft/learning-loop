// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace Tests.E2ETools
{
    class FileSystemFileFetcher : BaseFileFetcher
    {
        public FileSystemFileFetcher(ILogParser logParser, TestContext testContext) : base(logParser, testContext)
        {
        }

        public string CookedLogsDirectory { get; set; }
        public string ModelFileDirectory { get; set; }

        public override string TryDownloadCookedLogs(string cookedLogsDirectory, int? blobHour)
        {
            return TryDownloadLogs(cookedLogsDirectory, this.CookedLogName);
        }

        public override string TryDownloadSkippedLogs(string cookedLogsDirectory, int? blobHour)
        {
            return TryDownloadLogs(cookedLogsDirectory, this.SkippedLogName, subPath: AzureBlobConstants.SkippedLogsDirectoryPrefix);
        }

        public override string DownloadModelFile(bool clientModel = true)
        {
            if (File.Exists(this.CurrentModelName))
            {
                File.Delete(this.CurrentModelName);
            }
            if (clientModel)
            {
                File.Copy(Path.Combine(ModelFileDirectory, "current"), this.CurrentModelName);
                return Path.Combine(ModelFileDirectory, "current");
            }
            else
            {
                File.Copy(Path.Combine(ModelFileDirectory, "currenttrainer"), this.CurrentModelName);
                return Path.Combine(ModelFileDirectory, "currenttrainer");
            }
        }

        private string TryDownloadLogs(string cookedLogsDirectory, string logName, string subPath = AzureBlobConstants.CookedLogsDirectoryPrefix)
        {
            if (cookedLogsDirectory == null)
            {
                cookedLogsDirectory = CookedLogsDirectory;
            }

            string filename = Path.Combine(cookedLogsDirectory, PathHelper.BuildBlobName(LastConfEditDate, DateTime.UtcNow.Date, 0, subPath: subPath));

            if (!File.Exists(filename))
            {
                return string.Empty;
            }

            if (File.Exists(logName))
            {
                File.Delete(logName);
            }

            File.Copy(filename, logName);

            using (var reader = new StreamReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
