// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Billing;

namespace Microsoft.DecisionService.Common;

public class NullBillingClient : IBillingClient
{
    public void ReportUsage(long count)
    {
    }
}