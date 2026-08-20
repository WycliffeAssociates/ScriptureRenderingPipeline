using Microsoft.Extensions.Configuration;
using PipelineCommon.Helpers;

namespace ScriptureRenderingPipelineWorker;

public class GiteaClientFactory
{
    private Dictionary<string, GiteaConfiguration> _configurations;
    private Dictionary<string, GiteaClient> _clients = new();
    private SemaphoreSlim _semaphore = new(1, 1);
    public GiteaClientFactory(IConfiguration configuration)
    {
        _configurations = configuration.GetSection("Gitea").Get<Dictionary<string, GiteaConfiguration>>() ?? throw new InvalidOperationException("Missing Gitea configuration");
    }

    public GiteaClient CreateClient(string config)
    {
        config = config.ToLower();
        _semaphore.Wait();
        try
        {
            
            if (_clients.TryGetValue(config, out var cachedClient))
            {
                return cachedClient;
            }

            if (!_configurations.TryGetValue(config, out var giteaConfig))
            {
                throw new InvalidOperationException($"Missing Gitea configuration for {config}");
            }

            var client = new GiteaClient(giteaConfig.BaseUrl, giteaConfig.User, giteaConfig.Password);
            _clients[config] = client;
            return client;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

internal class GiteaConfiguration
{
    public string BaseUrl { get; set; }
    public string User { get; set; }
    public string Password { get; set; }
}