using System.Text.Json.Serialization;

namespace PipelineCommon.Models.Webhook
{
    public class WebhookRegistrationResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("deleteUrl")]
        public string DeleteUrl { get; set; } = string.Empty;
    }

    public class WebhookDeletionResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}
