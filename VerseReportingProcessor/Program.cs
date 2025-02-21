using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace VerseReportingProcessor;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddUserSecrets<VerseCounterService>();
        var applicationInsightsSet = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING") != null;
        builder.Services.AddHostedService<VerseCounterService>();
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<VerseProcessorMetrics>();
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(nameof(VerseCounterService)));
            options.IncludeScopes = true;
            options.ParseStateValues = true;
            options.AddOtlpExporter();
            if (applicationInsightsSet)
            {
                options.AddAzureMonitorLogExporter();
            }
        });
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(nameof(VerseCounterService));
            })
            .WithMetrics(metrics =>
            {
                metrics.AddOtlpExporter();
                metrics.AddMeter(nameof(VerseCounterService));
                if (applicationInsightsSet)
                {
                    metrics.AddAzureMonitorMetricExporter();
                }
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(nameof(VerseCounterService));
                tracing.SetErrorStatusOnException();
                tracing.AddHttpClientInstrumentation();
                tracing.AddSqlClientInstrumentation();
                tracing.AddOtlpExporter();
                if (applicationInsightsSet)
                {
                    tracing.AddAzureMonitorTraceExporter();
                }
            });
        var host = builder.Build();
        await host.RunAsync();
    }
}