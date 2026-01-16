using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Azure;
using PipelineCommon.Models;
using PipelineCommon.Models.Webhook;
namespace PipelineCommon.Helpers;
/// <summary>
/// Azure Table Storage implementation of the webhook service.
/// </summary>
public class AzureStorageWebhookStorage : IWebhookService
{
    private readonly TableServiceClient _tableServiceClient;
    public AzureStorageWebhookStorage(IAzureClientFactory<TableServiceClient> tableServiceClientFactory)
    {
        _tableServiceClient = tableServiceClientFactory.CreateClient("WebhookTableClient");
    }
    public async Task<string> RegisterWebhookAsync(WebhookDefinition webhook)
    {
        var tableClient = _tableServiceClient.GetTableClient("Webhooks");
        await tableClient.CreateIfNotExistsAsync();
        var webhookEntity = new WebhookEntity
        {
            PartitionKey = "Webhook",
            RowKey = Guid.NewGuid().ToString(),
            Url = webhook.Url,
            EventType = webhook.EventType
        };
        await tableClient.AddEntityAsync(webhookEntity);
        return webhookEntity.RowKey;
    }
    public async Task UnregisterWebhookAsync(string webhookId)
    {
        var tableClient = _tableServiceClient.GetTableClient("Webhooks");
        await tableClient.DeleteEntityAsync("Webhook", webhookId);
    }

    public async Task<bool> WebhookExistsAsync(string webhookId)
    {
        var tableClient = _tableServiceClient.GetTableClient("Webhooks");
        try
        {
            var response = await tableClient.GetEntityAsync<WebhookEntity>("Webhook", webhookId);
            return response != null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }


    public async Task<IEnumerable<WebhookDefinition>> GetWebhooksAsync()
    {
        var tableClient = _tableServiceClient.GetTableClient("Webhooks");
        await tableClient.CreateIfNotExistsAsync();
        var query = tableClient.QueryAsync<WebhookEntity>(e => e.PartitionKey == "Webhook");
        var webhooks = new List<WebhookDefinition>();
        await foreach (var entity in query.ConfigureAwait(false))
        {
            if (!string.IsNullOrWhiteSpace(entity.Url))
            {
                webhooks.Add(entity);
            }
        }
        return webhooks;
    }
}
internal class WebhookEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string Url { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public static implicit operator WebhookDefinition(WebhookEntity input)
    {
        return new WebhookDefinition
        {
            Url = input.Url,
            EventType = input.EventType
        };
    }
}
