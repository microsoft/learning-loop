// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.DecisionService.Instrumentation
{
    public class MetricsUtil
    {
        public const string AppIdKey = "appId";
        public const string ProblemTypeKey = "ProblemType";
        public const string ModeKey = "Mode";
        public const string RunIdKey = "RunId";
        public const string ResourceIdKey = "ResourceId";
        public const string IsMatchBaselineKey = "IsMatchBaseline";
        public const string ProblemSubTypeKey = "SubType";
        public const string ApiKey = "Api";
        public const string LogApiKey = "Log";

        public static Dictionary<string, string> GetDefaultProperties(string appId=null, string problemType=null)
        {
            return  new Dictionary<string, string>()
            {
                { AppIdKey, string.IsNullOrEmpty(appId) ? "unknown" : appId },
                { ProblemTypeKey,  string.IsNullOrEmpty(problemType)  ? "unknown" : problemType.ToLower()},
            };
        }

        public static IEnumerable<KeyValuePair<string, object?>> GetDefaultPropertiesV2(string appId, string problemType)
        {
            return new [] {
                new KeyValuePair<string,object?>(AppIdKey,string.IsNullOrEmpty(appId) ? "unknown" : appId),
                new KeyValuePair<string,object?>(ProblemTypeKey,string.IsNullOrEmpty(problemType)  ? "unknown" : problemType.ToLower())
            };
        }


public static IEnumerable<KeyValuePair<string, object?>> GetDefaultPropertiesV2(string appId=null)
        {
            return new [] {
                new KeyValuePair<string,object?>(AppIdKey,string.IsNullOrEmpty(appId) ? "unknown" : appId)
            };
        }

        public static Dictionary<string, string> GetLearningMetricsProperties(string resourceId, DateTime lastConfigEditDate, string mode = null)
        {
            return new Dictionary<string, string>()
            {
                { ResourceIdKey, resourceId },
                { RunIdKey, lastConfigEditDate.ToString("s") },
                { ModeKey,  string.IsNullOrEmpty(mode)  ? "Online"  : mode },
            };
        }
    }
}