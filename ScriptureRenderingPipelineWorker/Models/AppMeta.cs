using System.Text.Json.Serialization;

namespace ScriptureRenderingPipelineWorker.Models;

public class AppMeta
{
   [JsonPropertyName("fontUrl")]
    public string FontUrl { get; set; }
   [JsonPropertyName("fontFamily")]
    public string FontFamily { get; set; }
}