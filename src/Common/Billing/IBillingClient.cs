// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.DecisionService.Common.Billing
{
    public interface IBillingClient
    {
        public void ReportUsage(long count);
    }
}
