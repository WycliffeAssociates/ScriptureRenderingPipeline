using System.Text.Json.Serialization;

namespace ScriptureRenderingPipelineWorker.Models;

public class OutputBook
{
	[JsonPropertyName("slug")]
	public string Slug { get; set; }
	[JsonPropertyName("label")]
	public string Label { get; set; }
	[JsonPropertyName("chapters")]
	public List<OutputChapters> Chapters { get; set; }

	[JsonPropertyName("lastRendered")]
	public string LastRendered { get; set; }

	public OutputBook()
	{
		Chapters = new List<OutputChapters>();
	}
}