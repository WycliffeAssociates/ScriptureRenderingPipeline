using Newtonsoft.Json;

namespace PipelineCommon.Models.Webhook
{
    public class Author
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("email")]
        public string Email { get; set; }
        [JsonProperty("username")]
        public string Username { get; set; }
    }

}
