using System.Text.Json.Serialization;

namespace VerseReportingProcessor.Models;

/// <summary>
/// One chunk in a <c>chunks.json</c> file, in the Door43 format BTTWriterCatalog writes
/// </summary>
public class ChunkDefinition
{
    [JsonPropertyName("chp")]
    public string? Chapter { get; set; }
    [JsonPropertyName("firstvs")]
    public string? FirstVerse { get; set; }
}
