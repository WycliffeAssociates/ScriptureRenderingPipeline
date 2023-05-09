using System;
using System.Collections.Generic;
using System.Text.Json;
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
	[JsonPropertyName("repoUrl")]
	public string RepoUrl { get; set; }
	[JsonPropertyName("bible")]
	public List<OutputBook> Bible { get; set; }
	[JsonPropertyName("words")]
	public List<OutputWordCategory> Words { get; set; }
	[JsonPropertyName("navigation")]
	public List<OutputNavigation> Navigation { get; set; }
	[JsonPropertyName("downloadLinks")]
	public List<DownloadLink> DownloadLinks { get; set; }

	[JsonPropertyName("lastRendered")]
	public string LastRendered { get; set; }

	[JsonPropertyName("wholeResourceByteCount")]
	public long ByteCount { get; set; }


	[JsonPropertyName("appMeta")]
	public AppMeta AppMeta { get; set; }

}