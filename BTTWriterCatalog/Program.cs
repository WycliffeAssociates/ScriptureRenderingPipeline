using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddAzureClients(clientBuilder =>
        {
            clientBuilder.AddServiceBusClient(context.Configuration.GetValue<string>("ServiceBusConnectionString"))
                .WithName("ServiceBusClient");
            clientBuilder.AddBlobServiceClient(context.Configuration.GetValue<string>("BlobStorageConnectionString"))
                .WithName("BlobServiceClient");
        });
        // Add cosmosdb client
        services.AddSingleton<CosmosClient>(_ =>
        {
            var cosmosConnectionString = context.Configuration.GetValue<string>("DBConnectionString");
            return new CosmosClient(cosmosConnectionString);
        });
    })
    .Build();

host.Run();
