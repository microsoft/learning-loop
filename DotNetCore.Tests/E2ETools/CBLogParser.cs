// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace DotNetCore.Tests.E2ETools
{
    public class CBLogParser : ILogParser
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
                        EventId = json["EventId"].Value<string>()
                    };

                    if (line.Contains("RewardValue"))
                    {
                        log.EventType = EventType.RewardEvent;
                        log.RewardValue = json["RewardValue"].Value<double>();
                    }
                    else
                    {
                        log.EventType = EventType.JoinedEvent;
                        log.Outcomes = new List<Outcome>
                        {
                            new Outcome()
                            {
                                LabelCost = json["_label_cost"].Value<double>(),
                                LabelAction = json["_label_Action"].Value<int>()
                            }
                        };
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
