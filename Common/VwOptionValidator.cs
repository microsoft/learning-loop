// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.DecisionService.VowpalWabbit;

public class VwOptionValidator
{
    private struct DisallowedOption
    {
        public DisallowedOption(string longName, string? shortName = null)
        {
            LongName = longName;
            ShortName = shortName;
        }

        public string LongName { get; set; }
        public string? ShortName { get; set; }
    }

    private static DisallowedOption[] _disallowedOptions = new[]
    {
        new DisallowedOption("data", "d"),
        new DisallowedOption("initial_regressor", "i"),
        new DisallowedOption("final_regressor", "f"),
        new DisallowedOption("extra_metrics", null),
        new DisallowedOption("no_stdin", null),
        new DisallowedOption("dry_run", null),
        new DisallowedOption("id", null),
        new DisallowedOption("save_resume", null),
        new DisallowedOption("preserve_performance_counter", null),
        new DisallowedOption("predict_only_model", null),
        new DisallowedOption("dsjson", null),
        new DisallowedOption("binary_parser", null),
        new DisallowedOption("json", null),
        new DisallowedOption("quiet", null),
        new DisallowedOption("readable_model", null),
        new DisallowedOption("invert_hash", null),
        new DisallowedOption("driver_output_off", null),
        new DisallowedOption("driver_output", null),
        new DisallowedOption("log_level", null),
        new DisallowedOption("log_output", null),
        new DisallowedOption("limit_output", null),
    };
    
    private static bool ContainsOption(string args, string longName, string? shortName)
    {
        // Add a spaces around shortname to reduce the change of matching a prefix
        return args.Contains($"--{longName}") || (shortName != null && args.Contains($" -{shortName} "));
    }
    
    public static ValidationResult Validate(string? mlArgs)
    {
        if (mlArgs == null)
        {
            return new ValidationResult("MachineLearningArguments cannot be null.");
        }

        if (String.IsNullOrWhiteSpace(mlArgs))
        {
            return new ValidationResult("MachineLearningArguments cannot be empty.");
        }
        
        foreach (var disallowedOption in _disallowedOptions)
        {
            if (ContainsOption(mlArgs, disallowedOption.LongName, disallowedOption.ShortName))
            {
                return new ValidationResult($"Option {disallowedOption.LongName} is not allowed in the model arguments.");
            }
        }
        
        return ValidationResult.Success!;
    }
}