// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Data;
using System.Collections.Generic;
using System.Linq;

namespace Tests.E2ETools
{
    public abstract class BaseRequestSender
    {
        public const string Dangling_Reward_Id_Prefix = "dangling_reward-";

        public List<RequestRecord> Sent { get; } = new List<RequestRecord>();
        public List<RequestRecord> LearnableSent { get; } = new List<RequestRecord>();
        public List<RequestRecord> SkippedSent { get; } = new List<RequestRecord>();

        public double? ExpectedReward { get; private set; }

        public RewardFunction RewardFunc { get; private set; }

        public BaseRequestSender(double? expectedReward = null, RewardFunction func = RewardFunction.earliest)
        {
            this.ExpectedReward = expectedReward;
            this.RewardFunc = func;
        }

        public List<RequestRecord> RequestsWithoutErrors
        {
            get
            {
                return Sent.Except(RequestsWithErrors).ToList();
            }
        }

        public List<ErrorRequestRecord> RequestsWithTrueErrors
        {
            get
            {
                return RequestsWithErrors.Where(err => err.Expected == false).ToList();
            }
        }

        public List<RequestRecord> LearnableRequestsWithoutErrors
        {
            get
            {
                return LearnableSent.Except(RequestsWithErrors).ToList();
            }
        }

        public List<ErrorRequestRecord> LearnableRequestsWithTrueErrors
        {
            get
            {
                return LearnableSent.Intersect(RequestsWithErrors)
                    .Where(err => !((err as ErrorRequestRecord)?.Expected ?? false))
                    .Select(err => (ErrorRequestRecord)err).ToList();
            }
        }

        public List<RequestRecord> SkippedRequestsWithoutErrors
        {
            get
            {
                return SkippedSent.Except(RequestsWithErrors).ToList();
            }
        }

        public List<ErrorRequestRecord> SkippedRequestsWithTrueErrors
        {
            get
            {
                return SkippedSent.Intersect(RequestsWithErrors)
                    .Where(err => !((err as ErrorRequestRecord)?.Expected ?? false))
                    .Select(err => (ErrorRequestRecord)err).ToList();
            }
        }

        /// <summary>
        /// Send a pref-defined number of interaction/observation requests with expected reward using selected aggregation funciton.
        /// </summary>
        public void SendRequests(int numOfEventIds, int startId = 0, bool deferActivation = false, RewardType? rewardType = null)
        {
            SendRequestsWithExpectedReward(numOfEventIds, startId, deferActivation, this.ExpectedReward, rewardType, this.RewardFunc);
        }

        public double GetTotalRewardsSent()
        {
            return LearnableRequestsWithoutErrors.Sum(rr => rr.Rewards == null ? 0 : rr.Rewards[0].Value);
        }

        private List<ErrorRequestRecord> RequestsWithErrors
        {
            get
            {
                return Sent.Where(r => r.GetType() == typeof(ErrorRequestRecord)).Cast<ErrorRequestRecord>().ToList();
            }
        }

        public abstract void SendDanglingReward(int numOfEventIds, int danglingRewardStartIndex);

        public abstract void SendInvalidRanks();

        public abstract ApprenticeTestMetrics GetApprenticeModeMetrics();

        /// <summary>
        /// Send a pref-defined number of interaction/observation requests with expected reward using selected aggregation funciton.
        /// </summary>
        /// <param name="numOfEvent">number of Event Ids to generate.</param>
        /// <param name="startId">start Id of the generated Events.</param>
        /// <param name="deferActivation">deferActivation.</param>
        /// <param name="expectedReward">expected value. Use with aggreationFunc.</param>
        /// <param name="aggreationFunc">aggreation funciton to generated expected reward value.</param>
        protected abstract void SendRequestsWithExpectedReward(int numOfEventIds, int startId = 0, bool deferActivation = false, double? expectedReward = 0, RewardType? rewardType = null, RewardFunction? aggreationFunc = RewardFunction.earliest);
    }
}
