
using Microsoft.Graph;
using Microsoft.Azure.Functions.Worker;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

using Azure.Identity;

using App.Utils;
using App.Factory;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration(config => 
    {
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) => 
    {
        // Register configurate 
        var config = context.Configuration.Get<AppConfig>() ?? throw new InvalidOperationException("AppConfig could not be loaded");
        services.AddSingleton(config);
        
        // Register Application Insight
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Register Factory
        services.AddSingleton<EventHubProducerClientFactory>();
        services.AddSingleton<BlobContainerClientFactory>();

        // Register GraphServiceClient
        services.AddSingleton<GraphServiceClient>(sp => 
        {
            var config = sp.GetRequiredService<AppConfig>();

            var credentials = new ClientSecretCredential(config.TENANT_ID, config.CLIENT_ID, config.CLIENT_SECRET);
            string[] scopes = [$"{config.ApplicationScope}"];

            return new GraphServiceClient(credentials, scopes); 
        });
    })
    .Build();

host.Run();