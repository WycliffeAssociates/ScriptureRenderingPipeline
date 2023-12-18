using System.Text.Json.Serialization;

namespace ScriptureRenderingPipelineWorker.Models
{
    public class DownloadLink
    {
        [JsonPropertyName("link")]
        public string Link { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; }
    }
}