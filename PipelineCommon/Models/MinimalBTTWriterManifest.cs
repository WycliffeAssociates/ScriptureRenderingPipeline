using System.Text.Json.Serialization;
using BTTWriterLib;
using BTTWriterLib.Models;

namespace PipelineCommon.Models;

/// <summary>
/// A very minimal BTTWriter manifest that can be used to load a BTTWriter project with minimal chance of serialization failure
/// </summary>
public class MinimalBTTWriterManifest
{
    [JsonPropertyName("resource")]
    public MinimalBTTWriterManifestItem Resource { get; set; }
    [JsonPropertyName("project")]
    public MinimalBTTWriterManifestItem Project { get; set; }
    [JsonPropertyName("target_language")]
    public MinimalBTTWRriterManifestLanguage TargetLanguage { get; set; }
    [JsonPropertyName("finished_chunks")]
    public string[] FinishedChunks { get; set; }

    public BTTWriterManifest AsBTTWriterManifest()
    {
        return new BTTWriterManifest
        {
            resource = new IdNameCombo
            {
                id = Resource?.Id,
                name = Resource?.Name
            },
            target_language = new TargetLanguage
            {
                id = TargetLanguage?.Id,
                name = TargetLanguage?.Name,
                direction = TargetLanguage?.Direction
            },
            project = new IdNameCombo
            {
                id = Project?.Id,
                name = Project?.Name
            },
            finished_chunks = FinishedChunks
        };
    }
}
public class MinimalBTTWriterManifestItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("id")]
    public string Id { get; set; }
}

public class MinimalBTTWRriterManifestLanguage : MinimalBTTWriterManifestItem
{
    [JsonPropertyName("direction")]
    public string Direction { get; set; }
}
