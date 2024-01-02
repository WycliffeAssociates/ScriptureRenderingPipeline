using System.Text.Json.Serialization;

namespace PipelineCommon.Models.Webhook
{
    public class Committer
    {
       [JsonPropertyName("name")]
        public string Name { get; set; }
       [JsonPropertyName("email")]
        public string Email { get; set; }
       [JsonPropertyName("username")]
        public string Username { get; set; }
    }

}
