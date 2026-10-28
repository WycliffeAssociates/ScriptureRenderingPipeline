using System;

namespace PipelineCommon.Models.BusMessages;

/// <summary>
/// Per-repo fields shared with the nightly GogsAnalysis job.
/// </summary>
public class RepoAnalysisFields
{
	public string? BookId { get; set; }
	public string? ResourceType { get; set; }
	public string? Translators { get; set; }
	public DateTime? LastCommitDate { get; set; }
	/// <summary>
	/// Null when the repo has no BTT Writer manifest to list finished chunks.
	/// </summary>
	public int? FinishedChunks { get; set; }
	/// <summary>
	/// Null when the repo has no chunk files, such as a whole Bible in USFM.
	/// </summary>
	public int? UploadedChunks { get; set; }
}
