using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PipelineCommon.Models.BusMessages;

namespace ScriptureRenderingPipelineWorker;

/// <summary>
/// Azure Function trigger that listens for WACS messages and dispatches them to registered webhooks.
/// A most dutiful servant, this function processes incoming missives with utmost haste and care.
/// </summary>
public class WebhookDispatcherTrigger
{
    private readonly ILogger<WebhookDispatcherTrigger> _logger;
    private readonly WebhookDispatcher _webhookDispatcher;

    public WebhookDispatcherTrigger(
        ILogger<WebhookDispatcherTrigger> logger,
        WebhookDispatcher webhookDispatcher)
    {
        _logger = logger;
        _webhookDispatcher = webhookDispatcher;
    }

    /// <summary>
    /// Processes incoming WACS messages from the service bus and dispatches to webhooks.
    /// </summary>
    [Function("WebhookDispatcherTrigger")]
    public async Task RunAsync(
        [ServiceBusTrigger(
            "WACSEvent",
            "WebhookDispatcher",
            IsSessionsEnabled = false,
            Connection = "ServiceBusConnectionString"
        )] string rawMessage)
    {
        try
        {
            _logger.LogInformation("Received WACS message for webhook dispatching");

            var message = JsonSerializer.Deserialize(rawMessage, WorkerJsonContext.Default.WACSMessage);
            
            if (message is null)
            {
                _logger.LogWarning("Failed to deserialize WACS message");
                throw new InvalidOperationException("Failed to deserialize WACS message");
            }

            _logger.LogInformation(
                "Processing WACS message - EventType: {EventType}, Repository: {Repo}, User: {User}",
                message.EventType,
                message.Repo,
                message.User);

            await _webhookDispatcher.DispatchAsync(message);

            _logger.LogInformation("Webhook dispatching completed successfully for event type: {EventType}", message.EventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WACS message for webhook dispatch");
            throw;
        }
    }
}

