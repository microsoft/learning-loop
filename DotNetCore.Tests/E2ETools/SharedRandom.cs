// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace DotNetCore.Tests.E2ETools
{
    /// <summary>
    /// Singleton Random with shared seed (not thread safe)
    /// </summary>
    class SharedRandom
    {
        private static SharedRandom singleInstance = null;
        private System.Random systemRandom;
        public static int Seed { get; set; }

        private SharedRandom()
        {
            systemRandom = new Random(Seed);
        }

        public static SharedRandom Instance()
        {
            if (singleInstance == null)
            {
                singleInstance = new SharedRandom();
            }
            return singleInstance;
        }

        public static void ResetSeed(int seed)
        {
            Seed = seed;
            singleInstance = new SharedRandom();
        }

        public double NextDouble()
        {
            return systemRandom.NextDouble();
        }

        public int Next(int minValue, int maxValue)
        {
            return systemRandom.Next(minValue, maxValue);
        }

        public int Next()
        {
            return systemRandom.Next();
        }
    }
}
