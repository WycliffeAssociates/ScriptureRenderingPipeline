using System.Text.Json.Serialization;

namespace ScriptureRenderingPipelineWorker.Models;

public class OutputWordCategory
{
   [JsonPropertyName("slug")]
    public string Slug { get; set; }
   [JsonPropertyName("label")]
    public string Label { get; set; }
   [JsonPropertyName("words")]
    public List<OutputWord> Words { get; set; }

    public OutputWordCategory()
    {
        Words = new();
    }
}