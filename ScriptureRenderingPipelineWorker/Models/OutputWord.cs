
using System.Text.Json.Serialization;

namespace ScriptureRenderingPipelineWorker.Models;

public class OutputWord
{
   [JsonPropertyName("slug")]
    public string Slug { get; set; }
   [JsonPropertyName("label")]
    public string Label { get; set; }
}