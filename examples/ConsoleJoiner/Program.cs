// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Identity;
using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Billing;
using Microsoft.DecisionService.Common.Storage;
using Microsoft.DecisionService.Common.Storage.Azure;
using Microsoft.DecisionService.Common.Trainer;
using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.DecisionService.OnlineTrainer.Join;
using Microsoft.DecisionService.OnlineTrainer.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

static IHost CreateJoinerHost()
{
    var builder = Host.CreateApplicationBuilder();
    // add configuration sources
    builder.Configuration.AddJsonFile("appsettings.json", optional: true);
    // note: appsettings.user.json is not part of the repository. create your own
    //       to override settings in appsettings.json
    builder.Configuration.AddJsonFile("appsettings.user.json", optional: true);
    // setup logging from configuration (see sample appsettings.json)
    builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

    // AppIdConfig is required
    builder.Services.AddOptions<AppIdConfig>()
        .Bind(builder.Configuration)
        .ValidateDataAnnotations();
    // JoinerConfig is required for the joiner
    builder.Services.AddOptions<JoinerConfig>()
        .Bind(builder.Configuration)
        .ValidateDataAnnotations();
    // StorageConfig is required for the joiner
    builder.Services.AddOptions<StorageConfig>()
        .Bind(builder.Configuration)
        .ValidateDataAnnotations();
    // StorageBlockOptions is required for the joiner
    builder.Services.AddOptions<StorageBlockOptions>()
        .Bind(builder.Configuration)
        .ValidateDataAnnotations();

    // setup logging (requires for all standalone applications)
    builder.Services.AddSingleton(typeof(LoggerWithAppId<>));
    builder.Services.AddSingleton(typeof(LoggerWithAppId));
    builder.Services.AddSingleton(typeof(ILogger), typeof(Logger<Program>));

    // time provider is required by the joiner
    builder.Services.AddSingleton<ITimeProvider>(_ => SystemTimeProvider.Instance);
    // bill is required by the joiner
    builder.Services.AddSingleton<IBillingClient, NullBillingClient>();

    // setup OpenTelemetry (required for all standalone applications); using default here
    builder.Services.AddSingleton(TracerProvider.Default);

    // set up storage for the joiner
    builder.Services.AddSingleton<IStorageFactory, AzStorageFactory>(p => {
        var storageConfig = p.GetService<IOptions<StorageConfig>>() ?? throw new ApplicationException($"{nameof(StorageConfig)} is not configured");
        var storageUri = storageConfig.Value.StorageAccountUrl ?? throw new ApplicationException("StorageAccountUrl is set not");
        return new AzStorageFactory(storageUri, new DefaultAzureCredential());
    });

    builder.Services.AddSingleton<IBlobContainerClient>(p =>
    {
        var appId = p.GetService<IOptions<AppIdConfig>>() ?? throw new ApplicationException($"{nameof(AppIdConfig)} is not configured");
        var storageFactory = p.GetService<IStorageFactory>() ?? throw new ApplicationException("The storage factory was not created");
        return storageFactory.CreateBlobContainerClient(appId.Value.AppId);
    });

    // supply the joiner factory (this one uses EventHub)
    builder.Services.AddSingleton<IJoinerFactory>(_ => new JoinerEventHubFactory());
    builder.Services.AddHostedService<JoinerPipeline>();

    return builder.Build();
}

// demonstrate the application is running
static async Task StartExampleApplicationAsync(CancellationToken cancelToken)
{
    while (!cancelToken.IsCancellationRequested)
    {
        Console.WriteLine("{0:yyyy-MM-dd HH:mm:ss} The joiner is running", DateTime.Now);
        await Task.Delay(TimeSpan.FromSeconds(30), cancelToken);
    }
}

// build the host
var host = CreateJoinerHost();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

// handle Ctrl+C
var cancelTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (sender, e) =>
{
    logger.LogInformation("ConsoleJoiner received cancel signal");
    cancelTokenSource.Cancel();
    e.Cancel = true;
};

try
{
    logger.LogInformation("ConsoleJoiner example started");
    // get the configured container and create it if it doesn't exist
    var container = host.Services.GetService<IBlobContainerClient>() ?? throw new ApplicationException("failed to get a blob container client");
    await container.CreateIfNotExistsAsync();

    // start the joiner and other tasks
    var joinerTask = host.RunAsync(cancelTokenSource.Token);
    var appTask = StartExampleApplicationAsync(cancelTokenSource.Token);

    // wait for either task to complete
    var completedTask = await Task.WhenAny(joinerTask, appTask);
    if (completedTask.IsFaulted)
    {
        cancelTokenSource.Cancel();
    }
    await appTask;
}
catch (OperationCanceledException)
{
    logger.LogInformation("ConsoleJoiner example stopped");
}
catch (Exception e)
{
    Console.WriteLine($"Example ConsoleJoiner threw exception: {e.Message}");
}
finally
{
    Console.WriteLine("Example ConsoleJoiner exiting");
}
