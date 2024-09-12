// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Tests
{
    public static class Constants
    {
        public static readonly string SiteId = "1c69b1382c334de2b8e7978c04f72783";
        public static readonly string BaseUrl = "http://localhost:9025/MockCMS";
        public static readonly string DocumentPath = "/doc/";
        public const string ccbMlArgs = "--ccb_explore_adf --epsilon 0.4 --power_t 0 -l 0.001 --cb_type ips -q ::";
        public const string cbMlArgs = "--cb_explore_adf --epsilon 0.4 --power_t 0 -l 0.001 --cb_type ips -q ::";
        public const string caMlArgs = "--cats 4 --min_value=185 --max_value=23959 --bandwidth 1 --coin --loss_option 1 --json --quiet --epsilon 0.1 --id N/A";
    }
}
