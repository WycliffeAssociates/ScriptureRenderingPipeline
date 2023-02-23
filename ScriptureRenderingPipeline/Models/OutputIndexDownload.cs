using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ScriptureRenderingPipeline.Models;

public class DownloadIndex
{
	[JsonPropertyName("data")]
	public List<OutputBook> Data { get; set; }
}