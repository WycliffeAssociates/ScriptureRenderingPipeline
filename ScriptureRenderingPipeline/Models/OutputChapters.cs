using System.Text.Json.Serialization;

namespace ScriptureRenderingPipeline.Models;

public class OutputChapters
{
	[JsonPropertyName("number")]
	public string Number { get; set; }
	[JsonPropertyName("label")]
	public string Label { get; set; }
	[JsonPropertyName("content")]
	public string Content { get; set; }

	[JsonPropertyName("byteCount")]
	public int ByteCount { get; set; }
}