using System.Text.Json.Serialization;

namespace PipelineCommon.Models.Webhook
{
    public class Permissions
    {
       [JsonPropertyName("admin")]
        public bool Admin { get; set; }
       [JsonPropertyName("push")]
        public bool Push { get; set; }
       [JsonPropertyName("pull")]
        public bool Pull { get; set; }
    }

}
