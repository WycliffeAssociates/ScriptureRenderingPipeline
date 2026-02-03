using System.Collections.Generic;
using System.Threading.Tasks;
using PipelineCommon.Models;
using PipelineCommon.Models.Webhook;

namespace PipelineCommon;
/// <summary>
/// Service interface for managing outgoing webhooks, including registration, deletion, existence checks, and listing.
/// </summary>
public interface IWebhookService
{
    Task<string> RegisterWebhookAsync(WebhookDefinition webhook);
    Task UnregisterWebhookAsync(string webhookId);
    Task<bool> WebhookExistsAsync(string webhookId);
    Task<IEnumerable<WebhookDefinition>> GetWebhooksAsync();
}
