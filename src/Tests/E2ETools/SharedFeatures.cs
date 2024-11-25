// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Tests.E2ETools
{
    class SharedFeatures
    {
        public string Id { get; set; }
        public string Major { get; set; }
        public string Hobby { get; set; }
        public string FavoriteCharacter { get; set; }
        public Dictionary<String, Double> TopicClickProbability { get; set; }

        public SharedFeatures()
        {
        }

        public SharedFeatures(string id, string major, string hobby, string favoriteCharacter, Dictionary<String,Double> topicClickProbability) {
            Id = id;
            Major = major;
            Hobby = hobby;
            FavoriteCharacter = favoriteCharacter;
            TopicClickProbability = topicClickProbability;
        }

        public double SimulateOutcome(string chosenAction) {
            var rand = SharedRandom.Instance();
            if (rand.NextDouble() <= TopicClickProbability.GetValueOrDefault(chosenAction))
                return 1.0;
            else
                return 0.0;
        }
    }
}
