using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ScriptureRenderingPipeline.Models;

public class OutputIndex
{
    [JsonPropertyName("languageName")]
    public string LanguageName { get; set; }
    [JsonPropertyName("languageCode")]
    public string LanguageCode { get; set; }
    [JsonPropertyName("resourceType")]
    public string ResourceType { get; set; }
    [JsonPropertyName("resourceTitle")]
    public string ResourceTitle { get; set; }
    [JsonPropertyName("textDirection")]
    public string TextDirection { get; set; }
    [JsonPropertyName("bible")]
    public List<OutputBook> Bible { get; set; }
    [JsonPropertyName("repoUrl")]
    public string RepoUrl { get; set; }

    public OutputIndex()
    {
        Bible = new List<OutputBook>();
    }
}

public class OutputBook
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; }
    [JsonPropertyName("label")]
    public string Label { get; set; }
    [JsonPropertyName("chapters")]
    public List<OutputChapters> Chapters { get; set; }

    public OutputBook()
    {
        Chapters = new List<OutputChapters>();
    }
}

public class OutputChapters
{
    [JsonPropertyName("number")]
    public string Number { get; set; }
    [JsonPropertyName("label")]
    public string Label { get; set; }
}