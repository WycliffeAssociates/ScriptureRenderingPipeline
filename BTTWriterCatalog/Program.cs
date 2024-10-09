using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PipelineCommon.Helpers;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) => {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddAzureClients(clientBuilder =>
        {
            clientBuilder.AddBlobServiceClient(context.Configuration.GetValue<string>("BlobStorageConnectionString")).WithName("BlobStorageClient");
        });
    })
    .ConfigureLoggingForPipeline()
    .Build();

host.Run();
