using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace PipelineCommon.Helpers;

public static class AzureFunctionExtensions
{
    public static IHostBuilder ConfigureLoggingForPipeline(this IHostBuilder builder)
    {
        return builder.ConfigureLogging(logging =>
        {
            logging.AddFilter((provider, category, loglevel) =>
            {
                switch (category)
                {
                    case "Azure.Core" when loglevel < LogLevel.Warning:
                    case "Azure.Messaging.ServiceBus" when loglevel < LogLevel.Warning:
                        return false;
                    default:
                        return true;
                }
            });
            logging.Services.Configure<LoggerFilterOptions>(options =>
            {
                var defaultRule = options.Rules.FirstOrDefault(rule =>
                    rule.ProviderName ==
                    "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
                if (defaultRule is not null)
                {
                    options.Rules.Remove(defaultRule);
                }
            });
        });
    }
}