// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Tests.E2ETools
{
    public class SlotOutcome
    {
        [JsonProperty("_a")]
        public int[] Actions;

        [JsonProperty("_label_cost")]
        public double LabelCost;
    }

    public class CCBLogParser : ILogParser
    {
        /// <summary>
        /// Cooked logs parser
        /// </summary>
        public List<CookedLogLine> ParseLogs(string logs)
        {
            List<CookedLogLine> cookedLog = new List<CookedLogLine>();
            int id = 0;

            foreach (string line in logs.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    // validate that the output is valid JSON
                    JObject json = null;
                    try
                    {
                        json = JObject.Parse(line);
                    }
                    catch
                    {
                        //eventhub offsets are not a valid json, so just ignore this line
                        continue;
                    }

                    CookedLogLine log = new CookedLogLine()
                    {
                        Id = ++id,
                        EventId = json["EventId"].Value<string>(),
                        ProblemType = Microsoft.DecisionService.Common.ProblemType.CCB
                    };

                    if (line.Contains("RewardValue"))
                    {
                        log.EventType = EventType.RewardEvent;
                        log.RewardValue = json["RewardValue"].Value<double>();
                    }
                    else
                    {
                        //todo needs to be fixed
                        log.EventType = EventType.JoinedEvent;
                        log.Outcomes = new List<Outcome>();

                        var outcomesList = json["_outcomes"].ToObject<List<SlotOutcome>>();
                        foreach (SlotOutcome slotOutcome in outcomesList)
                        {
                            log.Outcomes.Add(new Outcome() { LabelCost = slotOutcome.LabelCost, LabelAction = slotOutcome.Actions[0] });
                        }
                        log.BaselineActions = json["_ba"].ToObject<List<int>>();
                    }

                    if (line.Contains("VWState"))
                    {
                        log.VWStateM = json["VWState"]["m"].Value<string>();
                    }

                    cookedLog.Add(log);
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to parse '{line}'", e);
                }
            }

            return cookedLog;
        }
    }
}