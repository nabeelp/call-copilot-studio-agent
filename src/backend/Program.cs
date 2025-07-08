// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.CopilotStudio.Client;
using CopilotStudioClientSample;
using CopilotStudioFunctionApp;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Get the configuration settings for the CopilotStudio client
        var connectionSettings = new SampleConnectionSettings(context.Configuration.GetSection("CopilotStudioClientSettings"));

        // Create an HTTP client for use by the CopilotStudio Client with pass-through token handler
        // This handler will extract the token from the incoming request and pass it through
        services.AddHttpClient("mcs").ConfigurePrimaryHttpMessageHandler(() => new PassThroughTokenHandler());

        // Register TokenExchangeService for OBO token exchange
        services.AddSingleton<TokenExchangeService>();

        // Add Settings and an instance of the CopilotStudio Client to the current services
        services
            .AddSingleton(connectionSettings)
            .AddTransient<CopilotClient>((serviceProvider) =>
            {
                var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<CopilotClient>();
                // Use the pass-through token approach instead of acquiring tokens in the function
                return new CopilotClient(connectionSettings, serviceProvider.GetRequiredService<IHttpClientFactory>(), logger, "mcs");
            });
    })
    .Build();

host.Run();


