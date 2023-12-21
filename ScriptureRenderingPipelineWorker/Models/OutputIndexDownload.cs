using System.Text.Json.Serialization;

namespace ScriptureRenderingPipelineWorker.Models;

public class DownloadIndex
{
	[JsonPropertyName("content")]
	public List<OutputBook> Content { get; set; }



	[JsonPropertyName("lastRendered")]
	public string LastRendered { get; set; }

	public DownloadIndex()
	{
		Content = new List<OutputBook>();
	}
}