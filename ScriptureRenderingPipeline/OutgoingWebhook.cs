using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PipelineCommon;
using PipelineCommon.Models;
using PipelineCommon.Models.Webhook;

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
    public async Task<HttpResponseData> Register([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
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
            response.Headers.Add("Content-Type", "application/json");
            var hostUrl = req.Url.Scheme + "://" + req.Url.Host + (req.Url.IsDefaultPort ? "" : $":{req.Url.Port}");
            var deleteUrl = $"{hostUrl}/UnregisterWebhook?id={webhookId}";
            var result = new WebhookRegistrationResponse
            {
                Id = webhookId,
                DeleteUrl = deleteUrl
            };
            await response.WriteStringAsync(JsonSerializer.Serialize(result));
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
     public async Task<HttpResponseData> Unregister([HttpTrigger(AuthorizationLevel.Anonymous, "delete")] HttpRequestData req)
    {
        try
        {
            var id = req.Query["id"];
            
            if (string.IsNullOrWhiteSpace(id))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync(JsonSerializer.Serialize(new WebhookDeletionResponse
                {
                    Success = false,
                    Message = "Webhook ID is required"
                }));
                return badResponse;
            }
            if (!await _webhookService.WebhookExistsAsync(id))
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync(JsonSerializer.Serialize(new WebhookDeletionResponse
                {
                    Success = false,
                    Message = "Webhook not found"
                }));
                return notFoundResponse;
            }

            await _webhookService.UnregisterWebhookAsync(id);
            _logger.LogInformation("Webhook unregistered successfully for ID: {Id}", id);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            var result = new WebhookDeletionResponse
            {
                Success = true
            };
            await response.WriteStringAsync(JsonSerializer.Serialize(result));
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

