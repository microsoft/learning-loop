// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.DecisionService.Common.Trainer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.DecisionService.OnlineTrainer;

public class LoggerWithAppId<T> : IDisposable where T : class
{
    private readonly IDisposable _scope;
    public ILogger<T> Instance { get; }

    public LoggerWithAppId(ILogger<T> logger, IOptions<AppIdConfig> appId)
    {
        Instance = logger;
        _scope = logger.BeginScope("{AppId}", appId.Value.AppId);
    }

    public void Dispose() => _scope.Dispose();
}

public class LoggerWithAppId : IDisposable
{
    private readonly IDisposable _scope;
    public ILogger Instance { get; }

    public LoggerWithAppId(ILogger logger, IOptions<AppIdConfig> appId)
    {
        Instance = logger;
        _scope = logger.BeginScope("{AppId}", appId.Value.AppId);
    }

    public void Dispose() => _scope.Dispose();
}