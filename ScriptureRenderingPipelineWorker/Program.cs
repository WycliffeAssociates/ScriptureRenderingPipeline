using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PipelineCommon;
using PipelineCommon.Helpers;
using ScriptureRenderingPipelineWorker;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddAzureClients(clientBuilder =>
        {
            clientBuilder.AddServiceBusClient(context.Configuration.GetValue<string>("ServiceBusConnectionString")).WithName("ServiceBusClient");

            foreach (var topic in ServiceBusTopics.All)
            {
                clientBuilder.AddClient<ServiceBusSender, ServiceBusClientOptions>((_, _, provider) =>
                        provider.GetRequiredService<IAzureClientFactory<ServiceBusClient>>()
                            .CreateClient("ServiceBusClient")
                            .CreateSender(topic))
                    .WithName(topic);
            }
            clientBuilder.AddBlobServiceClient(context.Configuration.GetValue<string>("ScripturePipelineStorageConnectionString")).WithName("BlobServiceClient");
            clientBuilder.AddTableServiceClient(context.Configuration.GetValue<string>("WebhookStorageConnectionString")).WithName("WebhookTableClient");
        });
        services.AddHttpClient("WebhookDispatcher", config =>
        {
            config.DefaultRequestHeaders.Add("User-Agent", "ScriptureRenderingPipeline/WebhookDispatcher");
        });
        
        // Register webhook services
        services.AddSingleton<GiteaClientFactory>();
        services.AddScoped<IWebhookService, AzureStorageWebhookStorage>();
        services.AddScoped<WebhookDispatcher>();
    })
    .ConfigureLoggingForPipeline()
    .Build();

host.Run();
