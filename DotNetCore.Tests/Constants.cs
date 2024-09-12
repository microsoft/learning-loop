// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace DotNetCore.Tests
{
    internal class Constants
    {
        public static readonly string LocalClientEndpoint = "http://localhost:5000";
        public static readonly Uri LocalClientStatusUrl = new Uri(new Uri(LocalClientEndpoint), "status");

        // Sample CB evaluation job sets
        public static readonly JobData EvaluationsSmallData = new JobData { AppId = "sample-cb", StartDate = DateTime.Parse("2022-01-10"), EndDate = DateTime.Parse("2022-01-11") };

        // Sample CCB evaluation job sets
        public static readonly JobData CCBDataWithSlotId = new JobData { AppId = "sample-ccb", StartDate = DateTime.Parse("2022-05-24"), EndDate = DateTime.Parse("2022-05-25") }; //~ 22 MB

        // Sample CCB corrupt evaluation job sets
        public static readonly JobData CCBEmptyDataWithSlotId = new JobData { AppId = "sample-ccb", StartDate = DateTime.Parse("2022-07-24"), EndDate = DateTime.Parse("2022-07-25") }; //~ 0 MB

        //ML Args used to generate trainerModel-100 model file
        public static readonly string MLArgsOfTrainerModel_100 = "--cb_adf --cb_explore_adf --cb_max_cost 1 --cb_min_cost 0 --cb_type ips --csoaa_ldf multiline --csoaa_rank --epsilon 1 --hash_seed 0 --lambda -1 --link identity --mellowness 0.100000001490116 --psi 1 --quadratic OE --quadratic GS --quadratic OS";

        public class JobData
        {
            public string AppId;
            public DateTime StartDate;
            public DateTime EndDate;
        }
    }
}
