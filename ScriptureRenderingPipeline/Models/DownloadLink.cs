using System.Text.Json.Serialization;

namespace ScriptureRenderingPipeline.Models
{
    public class DownloadLink
    {
        [JsonPropertyName("link")]
        public string Link { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; }
    }
}