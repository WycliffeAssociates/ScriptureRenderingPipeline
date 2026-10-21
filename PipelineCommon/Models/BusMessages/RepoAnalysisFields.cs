using System;

namespace PipelineCommon.Models.BusMessages;

/// <summary>
/// Per-repo fields shared with the nightly GogsAnalysis job.
/// </summary>
public class RepoAnalysisFields
{
	public string? BookId { get; set; }
	public string? ResourceType { get; set; }
	public string? Level { get; set; }
	public string Translators { get; set; } = string.Empty;
	public DateTime? LastCommitDate { get; set; }
	public int FinishedChunks { get; set; }
	public int UploadedChunks { get; set; }
}
