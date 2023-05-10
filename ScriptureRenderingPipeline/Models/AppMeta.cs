using System.Text.Json.Serialization;

namespace ScriptureRenderingPipeline.Models;

public class AppMeta
{
    [JsonPropertyName("fontUrl")]
    public string FontUrl { get; set; }
    [JsonPropertyName("fontFamily")]
    public string FontFamily { get; set; }
}