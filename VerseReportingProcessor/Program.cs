using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace VerseReportingProcessor;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddHostedService<VerseCounterService>();
        builder.Services.AddMemoryCache();
        var host = builder.Build();
        await host.RunAsync();
    }
}