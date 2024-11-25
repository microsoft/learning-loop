// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.DecisionService.Common
{
    public static class Constants
    {
        
        public const string CONFIG_DELIMITER = ":";
        public const string PREFIX_DELIMITER = "-";
        public const string TABLEPROPERTY_SECTION_DELIMITER = "__"; // Azure tables does not allow colon or dashes in the property name. So we use dunder.
        public const string TABLEPROPERTY_PREFIX_DELIMITER = "_"; // Azure tables does not allow colon or dashes in the property name. So we use underscore.
        
        /// <summary>
        /// The cookie key of the user cookie.
        /// </summary>
        public static readonly string CookieUserKey = "pid";

        /// <summary>
        /// The cookie value of the user cookie "dnt" for "do not track".
        /// </summary>
        public static readonly string CookieDoNotTrack = "dnt";

        public static readonly string DsAuthorizationHeader = "X-DS-Authorization";

        // APIM expects clients to provide header field 'Ocp-Apim-Subscription-Key' for subscribers to identify themselves.
        // APIM translates subscriberID into an AppID before forwarding calls to us with a new header 'apim-subscription-id'.
        public static readonly string ApimKeyHeader = "Ocp-Apim-Subscription-Key";

        // Header containing AppID.  Also see ApimKeyHeader.
        public static readonly string AppIdHeaderName = "apim-subscription-id";

        public static readonly string ServiceName = "Personalizer";

        public static readonly string AzureAuthConnectionStringEnvVar = "AzureServicesAuthConnectionString";

        /// <summary>
        /// Default EventHub message retention period
        /// </summary>
        public static readonly int MessageRetentionInDaysDefault = 2;

        /// <summary>
        /// Name of the interaction eventhub
        /// </summary>
        public static readonly string InteractionEventHubName = "interaction";

        /// <summary>
        /// Name of the observation eventhub
        /// </summary>
        public static readonly string ObservationEventHubName = "observation";

        /// <summary>
        /// The default max number of actions allowed in rank requests
        /// </summary>
        public const int DefaultMaximumActionsLength = 50;

        /// <summary>
        /// The default max depth of one episode in rank requests
        /// </summary>
        public const int DefaultMaxEpisodeDepth = 10;
    }
}
