using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PipelineCommon.Helpers;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddAzureClients(clientBuilder =>
        {
            clientBuilder.AddServiceBusClient(context.Configuration.GetValue<string>("ServiceBusConnectionString")).WithName("ServiceBusClient");
        });
    })
    .ConfigureLoggingForPipeline()
    .Build();

host.Run();
