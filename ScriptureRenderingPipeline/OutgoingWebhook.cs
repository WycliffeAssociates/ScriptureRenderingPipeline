using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ScriptureRenderingPipeline;

public class OutgoingWebhook
{
    private readonly ILogger<OutgoingWebhook> _logger;
    private readonly IWebhookService _webhookService;

    public OutgoingWebhook(ILogger<OutgoingWebhook> logger, IWebhookService webhookService)
    {
        _logger = logger;
        _webhookService = webhookService;
    }

    [Function("RegisterWebhook")]
    public async Task<HttpResponseData> Register([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var webhookDefinition = JsonSerializer.Deserialize<WebhookDefinition>(requestBody);
            
            if (webhookDefinition == null || string.IsNullOrWhiteSpace(webhookDefinition.Url))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid webhook definition: URL is required");
                return badResponse;
            }

            var webhookId = await _webhookService.RegisterWebhookAsync(webhookDefinition);
            _logger.LogInformation("Webhook registered successfully with ID: {WebhookId} for URL: {Url}", webhookId, webhookDefinition.Url);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(webhookId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering webhook");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error registering webhook");
            return errorResponse;
        }
    }
    [Function("UnregisterWebhook")]
     public async Task<HttpResponseData> Unregister([HttpTrigger(AuthorizationLevel.Function, "delete")] HttpRequestData req)
    {
        try
        {
            var id = req.Query["id"];
            
            if (string.IsNullOrWhiteSpace(id))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Webhook ID is required");
                return badResponse;
            }

            await _webhookService.UnregisterWebhookAsync(id);
            _logger.LogInformation("Webhook unregistered successfully for ID: {Id}", id);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Webhook unregistered successfully");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering webhook");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error unregistering webhook");
            return errorResponse;
        }
    }

}

public class WebhookDefinition
{
    public string Url { get; set; }
    public string EventType { get; set; }
}

public interface IWebhookService
{
    public Task<string> RegisterWebhookAsync(WebhookDefinition webhook);
    public Task UnregisterWebhookAsync(string id);
    public Task<IEnumerable<WebhookDefinition>> GetWebhooksAsync();
}
