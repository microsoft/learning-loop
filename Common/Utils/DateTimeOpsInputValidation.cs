// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.DecisionService.Common.Utils
{
    public class DateTimeOpsInputValidation
    {
        /// <summary>
        /// verfies the numberOfDays to be subtracted using AddDays DateTime function and bounds incase of exception
        /// </summary>
        public static DateTime SafeSubtractDays(DateTime date, double numbserOfDays)
        {
            try
            {
                return date.AddDays(-numbserOfDays);
            }
            catch (ArgumentOutOfRangeException)
            {
                return numbserOfDays >= 0 ? DateTime.MinValue : DateTime.MaxValue;
            }
        }

        /// <summary>
        /// verfies the numberOfDays to be added using AddDays DateTime function and bounds incase of exception
        /// </summary>
        public static DateTime SafeAddDays(DateTime date, double numbserOfDays)
        {
            try
            {
                return date.AddDays(numbserOfDays);
            }
            catch (ArgumentOutOfRangeException)
            {
                return numbserOfDays >= 0 ? DateTime.MaxValue : DateTime.MinValue;
            }
        }

    }
}
