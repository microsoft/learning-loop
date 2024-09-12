// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Trainer;
using Microsoft.DecisionService.Common.Utils.Exe;
using Microsoft.DecisionService.Instrumentation;
using Microsoft.VW.VWBinResolver;

namespace Microsoft.DecisionService.VowpalWabbit
{
    /// <summary>
    /// <see cref="IOnlineTrainer"/> implementation that uses VW exe to train a model.
    /// </summary>
    public class VwRunner : IOnlineTrainer
    {
        private readonly IExeRunner _exeRunner;
        private readonly string _baseMachineLearningArgs;
        private readonly ProblemType _problemType;

        public VwRunner(string machineLearningArgs) : this(machineLearningArgs,
            new CommandLineExeRunner(Resolver.ResolveVwBinary()))
        {
        }

        // We're going to ASSUME this has been fed through "validate" vw args already.
        public VwRunner(string machineLearningArgs, IExeRunner exeRunner)
        {
            this._exeRunner = exeRunner ?? throw new ArgumentNullException(nameof(exeRunner));
            this._baseMachineLearningArgs =
                machineLearningArgs ?? "";
            _problemType = MachineLearningArgsHelper.ExtractProblemType(machineLearningArgs);
        }

        public async Task<IOnlineTrainer.LearnResult> LearnAsync(
            byte[] joinedLogData,
            byte[]? inputModel,
            JoinedLogFormat format,
            CancellationToken cancellationToken = default)
        {
            using var dataTempFile = new TempFile();
            using var modelInputTempFile = new TempFile();
            using var modelOutputTempFile = new TempFile();
            using var extraMetricsFile = new TempFile();

            var argsBuilder = new StringBuilder(_baseMachineLearningArgs);
            argsBuilder.Append(
                $" --no_stdin --data={dataTempFile.FilePath} --extra_metrics={extraMetricsFile.FilePath} --final_regressor={modelOutputTempFile.FilePath}");
            argsBuilder.Append(" --save_resume --preserve_performance_counters");
            if (format == JoinedLogFormat.DSJSON)
            {
                argsBuilder.Append(" --dsjson");
            } else
            {
                argsBuilder.Append(" --binary_parser");
            }

            switch (_problemType) {
                case ProblemType.CB:
                    argsBuilder.Append(" --problem_type=cb");
                    break;
                case ProblemType.CCB:
                    argsBuilder.Append(" --problem_type=ccb");
                    break;
                case ProblemType.SLATES:
                    argsBuilder.Append(" --problem_type=slates");
                    break;
                case ProblemType.CA:
                    argsBuilder.Append(" --problem_type=ca");
                    break;
                default:
                    throw new NotImplementedException($"Problem type {_problemType} not implemented");
            }

            await File.WriteAllBytesAsync(dataTempFile.FilePath, joinedLogData, cancellationToken);
            if (inputModel != null)
            {
                argsBuilder.Append($" --initial_regressor={modelInputTempFile.FilePath}");
                await File.WriteAllBytesAsync(modelInputTempFile.FilePath, inputModel, cancellationToken);
            }

            var result = await _exeRunner.RunAsync(argsBuilder.ToString(), null, 1000, cancellationToken: cancellationToken);

            if (result.ExitCode != 0)
            {
                var interpretedResult = VWExeOutputInterpreter.InterpretOutput(result);
                throw TrainerException.CreateFromVwOutput("Failed to train model", interpretedResult);
            }

            var outputModel = await File.ReadAllBytesAsync(modelOutputTempFile.FilePath, cancellationToken);

            var metricsParser =
                LearnMetricsParserFactory.Create(_problemType);
            var metrics = metricsParser.Parse(extraMetricsFile.FilePath);
            return new IOnlineTrainer.LearnResult
            {
                Metrics = metrics,
                FinalModel = outputModel
            };
        }

        public async Task<byte[]> ConvertToInferenceModelAsync(
            byte[] inputModel,
            string modelId,
            CancellationToken cancellationToken = default)
        {
            using var modelInputTempFile = new TempFile();
            using var modelOutputTempFile = new TempFile();

            // TODO add id?
            var argsBuilder = new StringBuilder(_baseMachineLearningArgs);
            argsBuilder.Append(
                $" --no_stdin --initial_regressor={modelInputTempFile.FilePath} --final_regressor={modelOutputTempFile.FilePath} --id={modelId}");
            argsBuilder.Append(" --predict_only_model");

            await File.WriteAllBytesAsync(modelInputTempFile.FilePath, inputModel, cancellationToken);

            var result = await _exeRunner.RunAsync(argsBuilder.ToString(), cancellationToken: cancellationToken);

            if (result.ExitCode != 0)
            {
                var interpretedResult = VWExeOutputInterpreter.InterpretOutput(result);
                throw TrainerException.CreateFromVwOutput("Failed to convert model", interpretedResult);
            }

            return await File.ReadAllBytesAsync(modelOutputTempFile.FilePath, cancellationToken);
        }

        // Really all it does is add id
        public async Task<byte[]> ExportTrainerModelAsync(
            byte[] inputModel,
            string modelId,
            CancellationToken cancellationToken = default)
        {
            using var modelInputTempFile = new TempFile();
            using var modelOutputTempFile = new TempFile();

            // TODO add id?
            var argsBuilder = new StringBuilder(_baseMachineLearningArgs);
            argsBuilder.Append(
                $" --no_stdin --initial_regressor={modelInputTempFile.FilePath} --final_regressor={modelOutputTempFile.FilePath} --id={modelId}");
            argsBuilder.Append(" --save_resume --preserve_performance_counters");

            await File.WriteAllBytesAsync(modelInputTempFile.FilePath, inputModel, cancellationToken);

            var result = await _exeRunner.RunAsync(argsBuilder.ToString(), cancellationToken: cancellationToken);

            if (result.ExitCode != 0)
            {
                var interpretedResult = VWExeOutputInterpreter.InterpretOutput(result);
                throw TrainerException.CreateFromVwOutput("Failed to convert model", interpretedResult);
            }

            return await File.ReadAllBytesAsync(modelOutputTempFile.FilePath, cancellationToken);
        }



        public async Task<IOnlineTrainer.ValidationResult> ValidateModelAsync(byte[] modelData,
            CancellationToken cancellationToken = default)
        {
            using var modelInputTempFile = new TempFile();
            await File.WriteAllBytesAsync(modelInputTempFile.FilePath, modelData, cancellationToken);
            var argsBuilder = new StringBuilder(_baseMachineLearningArgs);
            argsBuilder.Append(
                $" --no_stdin --initial_regressor={modelInputTempFile.FilePath} ");
            var result = await _exeRunner.RunAsync(argsBuilder.ToString(), cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
            {
                var vwResults = VWExeOutputInterpreter.InterpretOutput(result);

                // Filter to error and critical lines
                var errorLines = vwResults.LogLines
                    .Where(line => line.TracingLevel is TracingLevel.Error or TracingLevel.Critical)
                    .Select(line => line.Message).ToList();
                return new IOnlineTrainer.ValidationResult
                {
                    IsValid = false,
                    Errors = errorLines
                };
            }

            return new IOnlineTrainer.ValidationResult
            {
                IsValid = true
            };
        }
    }
}
