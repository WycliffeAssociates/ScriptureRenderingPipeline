using System.Net.Http.Headers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PipelineCommon.Helpers;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddAzureClients(clientBuilder =>
        {
            clientBuilder.AddServiceBusClient(context.Configuration.GetValue<string>("ServiceBusConnectionString")).WithName("ServiceBusClient");
            clientBuilder.AddBlobServiceClient(context.Configuration.GetValue<string>("ScripturePipelineStorageConnectionString")).WithName("BlobServiceClient");
        });
        services.AddHttpClient("WACS", config =>
        {
            config.DefaultRequestHeaders.Add("User-Agent", "ScriptureRenderingPipeline");
        });
    })
    .ConfigureLoggingForPipeline()
    .Build();

host.Run();