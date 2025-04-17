using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace VerseReportingProcessor;

public class OrganizationServiceFactory
{
    private readonly ILogger<OrganizationServiceFactory> _logger;
    private ServiceClient? _serviceClient;
    private string _connectionString;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    public OrganizationServiceFactory(ILogger<OrganizationServiceFactory> logger, IConfiguration configuration)
    {
        _connectionString = configuration["ConnectionStrings:Dataverse"];
        _logger = logger;
    }

    public async Task<ServiceClient> GetServiceClientAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            _serviceClient ??= new ServiceClient(_connectionString);

            if (!_serviceClient.IsReady)
            {
                _serviceClient = new ServiceClient(_connectionString);
            }

            return _serviceClient;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}