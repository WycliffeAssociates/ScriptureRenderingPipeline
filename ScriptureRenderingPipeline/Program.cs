using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PipelineCommon.Helpers;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddAzureClients(clientBuilder =>
        {
            clientBuilder.AddServiceBusClient(context.Configuration.GetValue<string>("ServiceBusConnectionString")).WithName("ServiceBusClient");
        });
    })
    .ConfigureLoggingForPipeline()
    .Build();

host.Run();
