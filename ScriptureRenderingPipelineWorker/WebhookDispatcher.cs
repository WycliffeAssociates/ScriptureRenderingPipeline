using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Resilience;
using PipelineCommon;
using PipelineCommon.Models;
using PipelineCommon.Models.BusMessages;
using Polly;
using Polly.Retry;

namespace ScriptureRenderingPipelineWorker;

/// <summary>
/// Responsible for dispatching webhook notifications to registered endpoints with resilience and retry logic.
/// Forsooth, this noble service doth ensure thy webhooks reach their destinations most reliably.
/// </summary>
public class WebhookDispatcher
{
    private readonly ILogger<WebhookDispatcher> _logger;
    private readonly IWebhookService _webhookService;
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencePipeline;

    public WebhookDispatcher(
        ILogger<WebhookDispatcher> logger,
        IWebhookService webhookService,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _webhookService = webhookService;
        _httpClient = httpClientFactory.CreateClient("WebhookDispatcher");
        
        // Initialize resilience pipeline with retry strategy
        var retryPolicy = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => !r.IsSuccessStatusCode && r.StatusCode != HttpStatusCode.BadRequest),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(100),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .Build();
        
        _resiliencePipeline = retryPolicy;
    }


    /// <summary>
    /// Dispatches a WACS message to all registered webhooks matching the event type.
    /// </summary>
    public async Task DispatchAsync(WACSMessage message)
    {

        try
        {
            var webhooks = await _webhookService.GetWebhooksAsync();
            
            // Filter webhooks matching the event type
            var matchingWebhooks = webhooks
                .Where(w => string.Equals(w.EventType, message.EventType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!matchingWebhooks.Any())
            {
                _logger.LogInformation("No webhooks registered for event type: {EventType}", message.EventType);
                return;
            }

            _logger.LogInformation("Found {WebhookCount} webhooks for event type {EventType}", 
                matchingWebhooks.Count, message.EventType);

            // Dispatch to all matching webhooks in parallel
            var dispatchTasks = matchingWebhooks
                .Select(webhook => DispatchToWebhookAsync(webhook, message))
                .ToList();

            await Task.WhenAll(dispatchTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching webhooks for event type: {EventType}", message.EventType);
            throw;
        }
    }

    /// <summary>
    /// Dispatches a message to a specific webhook endpoint with resilience.
    /// </summary>
    private async Task DispatchToWebhookAsync(WebhookDefinition webhook, WACSMessage message)
    {
        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(message, WorkerJsonContext.Default.WACSMessage),
                System.Text.Encoding.UTF8,
                "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url)
            {
                Content = content
            };

            _logger.LogInformation("Dispatching webhook to {Url} for event type {EventType}", 
                webhook.Url, webhook.EventType);

            var response = await _resiliencePipeline.ExecuteAsync(
                async (ct) => await _httpClient.SendAsync(request, ct),
                CancellationToken.None);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully dispatched webhook to {Url} with status {StatusCode}", 
                    webhook.Url, response.StatusCode);
            }
            else
            {
                _logger.LogWarning("Webhook dispatch to {Url} returned status {StatusCode}", 
                    webhook.Url, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch webhook to {Url} after retries", webhook.Url);
        }
    }
}

