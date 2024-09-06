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
        builder.Services.AddHostedService<VerseCounterService>();
        builder.Services.AddMemoryCache();
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(nameof(VerseCounterService));
            })
            .WithTracing(tracing =>
            {
                tracing.AddOtlpExporter();
                tracing.AddInstrumentation(nameof(VerseCounterService));
            })
            .WithMetrics(metrics =>
            {
                metrics.AddOtlpExporter();
                metrics.AddMeter(nameof(VerseCounterService));
            });
        
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(nameof(VerseCounterService)));
            options.AddOtlpExporter();
            options.IncludeFormattedMessage = true;
        });
        var host = builder.Build();
        await host.RunAsync();
    }
}