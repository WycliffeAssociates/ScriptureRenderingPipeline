using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ScriptureRenderingPipeline.Models;

public class DownloadIndex
{
	[JsonPropertyName("content")]
	public List<OutputBook> Content { get; set; }

	public long ByteCount { get; set; }

	public DownloadIndex()
	{
		Content = new List<OutputBook>();
	}
}