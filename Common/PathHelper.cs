// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.DecisionService.Common
{
    public class PathHelper
    {
        public static bool ParseIndexAndDateFromBlob(string blobName, out int blobIndex, out int blobHour, out DateTime blobDate)
        {
            blobIndex = 0;
            blobHour = 0;
            blobDate = DateTime.MinValue;
            if (string.IsNullOrEmpty(blobName))
            {
                return false;
            }

            var regex = new Regex(@".*/(\d{4}/\d{2}/\d{2})_(\d+)(?:_(\d{2}))?\..*");
            var match = regex.Match(blobName);
            var success = match.Success;
            if (!success)
            {
                return false;
            }
            if (match.Groups.Count < 3)
            {
                return false;
            }
            success = DateTime.TryParseExact(match.Groups[1].Value, "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out blobDate);
            if (success)
            {
                blobIndex = int.Parse(match.Groups[2].Value);
                blobHour = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
            }
            return success;
        }

        public static void ParseIndexAndDate(string blobName, out int blobIndex, out int blobHour, out DateTime blobDate)
        {
            /*  example without hour information = "/myappid/20180923171200/data/2018/06/30_00.json" */
            /*  example with hour information = "/myappid/20180923171200/data/2018/06/30_00_01.json" */
            ParseIndexAndDateFromBlob(blobName, out blobIndex, out blobHour, out blobDate);
        }

        public static DateTime ParseDate(string blobName)
        {
            ParseIndexAndDate(blobName, out _, out _, out DateTime blobDate);
            return blobDate;
        }

        public static string BuildBlobListPrefix(DateTime lastConfigurationEditDate, string subPath = AzureBlobConstants.CookedLogsDirectoryPrefix)
        {
            return string.Format(CultureInfo.InvariantCulture,
                                "{0}/{1}",
                                lastConfigurationEditDate.ToString(ApplicationConstants.DateTimeStringFormat, CultureInfo.InvariantCulture), subPath);
        }

        public static string GetLogFormatExtension(JoinedLogFormat format)
        {
            switch (format)
            {
                case JoinedLogFormat.DSJSON:
                    return "json";
                case JoinedLogFormat.Binary:
                    return "fb";
                default:
                    throw new ArgumentException($"invalid file format: {format}", nameof(format));
            }
        }

        public static JoinedLogFormat GetLogFormatFromFileExtension(string fileExtension)
        {
            if (fileExtension == null)
            {
                throw new ArgumentNullException(nameof(fileExtension));
            }

            if (fileExtension.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                return JoinedLogFormat.DSJSON;
            }
            else if (fileExtension.Equals("fb", StringComparison.OrdinalIgnoreCase))
            {
                return JoinedLogFormat.Binary;
            }
            else
            {
                throw new ArgumentException($"File extension {fileExtension} is not a valid joined log format.", nameof(fileExtension));
            }
        }

        public static JoinedLogFormat GetLogFormatFromFilePath(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(extension))
            {
                throw new ArgumentException("File path for joined log cannot be null or empty.", nameof(filePath));
            }
            return GetLogFormatFromFileExtension(extension.Substring(1));
        }

        public static string BuildBlobName(DateTime lastConfigurationEditDate, DateTime blobDate, int blobIndex, string subPath = AzureBlobConstants.CookedLogsDirectoryPrefix, JoinedLogFormat format = JoinedLogFormat.DSJSON, int? blobHour = null)
        {
            // pad '0' to solve the blob sorting issue on the downloader side.
            // Previous behavior: ....28_1.json, 28_11.json, 28_2.json
            // pad buffer is setting to 10 digits now.
            // For previous data, the blob index will only increase after it has 4.75TB data.
            // Unless we have customer who has 4.75TB * 10 data per day, there is no migration issue.
            // https://docs.microsoft.com/en-us/rest/api/storageservices/understanding-block-blobs--append-blobs--and-page-blobs#about-block-blobs
            var builder = new StringBuilder();
            builder.AppendFormat(CultureInfo.InvariantCulture,
                        "{0}/{1}/{2:00}/{3:00}_{4:D10}",
                        BuildBlobListPrefix(lastConfigurationEditDate, subPath),
                        blobDate.Year,
                        blobDate.Month,
                        blobDate.Day,
                        blobIndex);
            if (blobHour.HasValue)
            {
                builder.AppendFormat("_{0:D2}", blobHour.Value);
            }
            builder.AppendFormat(".{0}",GetLogFormatExtension(format));
            return builder.ToString();
        }

        public static string BuildCheckpointName(DateTime dateFolder, string blobName)
        {
            return BuildCheckpointName(dateFolder.ToString(ApplicationConstants.DateTimeStringFormat, CultureInfo.InvariantCulture), blobName);
        }

        public static string BuildModelImportName(DateTime timeStamp)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0}/model-{1}.vw",
                AzureBlobConstants.ImportedModelsDirectory,
                timeStamp.ToString(ApplicationConstants.DateTimeStringFormat, CultureInfo.InvariantCulture));
        }

        public static string BuildCheckpointName(string dateFolder, string blobName)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0}/checkpoint/{1}",
                dateFolder,
                blobName);
        }
    }
}
