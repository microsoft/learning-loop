// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.DecisionService.Common
{
    public static class StorageUtilities
    {
        public static async Task<List<string>> ListDirectoriesAsync(
            IBlobContainerClient container,
            string initialPrefix = "")
        {
            var prefixes = new Queue<string>();
            prefixes.Enqueue(initialPrefix);
            var directoryNames = new List<string>();
            do
            {
                string currentPrefix = prefixes.Dequeue();
                foreach (var blobHierarchyItem in await container.GetBlobsByHierarchyAsync(currentPrefix, "/"))
                {
                    if (blobHierarchyItem.IsPrefix)
                    {
                        directoryNames.Add(blobHierarchyItem.Prefix);
                        prefixes.Enqueue(blobHierarchyItem.Prefix);
                    }
                }
            } while (prefixes.Count > 0);

            return directoryNames;
        }


        public static string BuildValidContainerName(string containerName)
        {
            if (String.IsNullOrEmpty(containerName))
            {
                throw new ArgumentNullException("containerName");
            }

            int maxLength = 63;
            int minLength = 3;
            // Ensure characters are alphanumeric (lower case only) or hyphens. Hyphens must not be consecutive.
            string newContainerName = Regex.Replace(Regex.Replace(containerName, "[^a-zA-Z0-9-]", ""), "-+", "-").ToLower();

            // Ensure the first or last character is not a hyphen ('-').
            newContainerName = newContainerName.Trim(new char[] { '-' });

            // Ensure name does not exceed maxLength
            newContainerName = newContainerName.Length <= maxLength ? newContainerName : newContainerName.Substring(0, maxLength);

            // Ensure name is atleast minLength
            newContainerName = newContainerName.Length >= minLength ? newContainerName : newContainerName.PadRight(minLength, '0');

            // TODO replace
            // NameValidator.ValidateContainerName(newContainerName);
            return newContainerName;
        }


        /// <summary>
        /// Check if the SasUri has the write permission to the container.
        /// Note: CloudBlobContainer created from SasUri does not have permissions to list permissions.
        /// </summary>
        /// <param name="blobContainerClient">the container</param>
        /// <param name="traceSession"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>true if it has write permission</returns>
        public static async Task<bool> IsContainerWritableAsync(IBlobContainerClient blobContainerClient, ILogger traceSession, CancellationToken cancellationToken)
        {
            var blockBlob = blobContainerClient.GetBlockBlobClient("checkwritepermissions.txt");

            try
            {
                var contentString = "Validating write permissions from personalizer instance for copy Log operation.";
                var contentStream = new MemoryStream(Encoding.UTF8.GetBytes(contentString));
                await blockBlob.WriteAsync(contentStream, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                // note: turns out that Azure Storage may throw an AggregateException instead of a RequestFailedException (wrapped by StorageException)
                // todo: is this problematic in other places such that it should be also be wrapped in a StorageException?
                traceSession?.LogInformation(ex, "Error while validating write permissions.");
                return false;
            }

            // write successful.  Try to cleanup the file we created
            try
            {
                await blockBlob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            }
            catch (Exception)
            {
                // do nothing. We made a best effort attempt to delete the blob that was created to validate write permissions.
            }

            return true;
        }

        /// <summary>
        /// Check if the SasUri has correct expiry date format for the backend(java/scala) storage sdk.
        /// Issue tracker: https://github.com/Azure/azure-storage-java/issues/573
        /// </summary>
        /// <param name="sasUri">The SasUri to the container.</param>
        /// <returns>true if it has the correct format</returns>
        public static bool ValidateSasUriDateFormat(Uri sasUri)
        {
            // example: https://dummy.blob.core.windows.net/dummy?se=2022-04-28T09:54:08Z&sp=rwl&sv=2021-04-10&sr=c&..
            string queryParam = sasUri.Query;
            if (string.IsNullOrEmpty(queryParam)) return false;
            var queryDict = HttpUtility.ParseQueryString(queryParam);

            bool Validator(string input)
            {
                if (queryDict[input] != null)
                {
                    string date = queryDict[input];
                    // https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings
                    // C# API TryParseExact does not parse the date format allowed by Java date time utility: 2021-11-24T17:31:48Z.
                    // Instead, it allows "2021-11-24T17:31:48.0000000Z"
                    // "DateTimeOffset.Parse, DateTime.Parse" reads more flexible format,
                    // which cannot be used for validation to tell 2021-11-24 is invalid format.
                    // 2021-11-24T17:31:48Z is the format generated in Azure portal.

                    if (!date.EndsWith('Z')) return false;
                    // Replace 'Z' with '000Z' to validate
                    string dateTrimed = date.TrimEnd('Z') + ".0000000Z";
                    if (DateTime.TryParseExact(dateTrimed, "o", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    // this function only validate the date format. For the integrity of the URL,
                    // please use funciton IsContainerSasUriWritableAsync in this file
                    return true;
                }
            }

            return Validator("se") && Validator("st");
        }


        public static async Task<DateTime> GetBlobLastModifyDateAsync(IBlobContainerClient container, string blobPath)
        {
            try
            {
                var blob = container.GetBlobClient(blobPath);
                if (!await blob.ExistsAsync())
                {
                    return default(DateTime);
                }
                var props = await blob.GetPropertiesAsync();
                return props.LastModified.DateTime;
            } catch (Exception)
            {
                return default(DateTime);
            }
        }
    }
}
