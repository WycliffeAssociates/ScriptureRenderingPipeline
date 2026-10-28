using System;
using System.Collections.Generic;

namespace PipelineCommon.Models.BusMessages;

public class VerseCountingResult
{
	public List<VerseCountingBook> Books { get; set; } = new();
	public bool Success { get; set; }
	public string Message { get; set; }
	public string LanguageCode { get; set; }
	public string User { get; set; }
	public string Repo { get; set; }
	public int RepoId { get; set; }
	public string RepoHtmlUrl { get; set; }
	// The fields below are also produced by the nightly GogsAnalysis job. They are null on messages from
	// older workers, and when the repo doesn't have them, so null means leave the PORT value alone.
	/// <summary>
	/// The book in the repo, or null when there isn't exactly one, such as a whole Bible.
	/// </summary>
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

	public VerseCountingResult()
	{
		
	}

	public VerseCountingResult(WACSMessage input)
	{
		User = input.User;
		Repo = input.Repo;
		RepoId = input.RepoId;
		RepoHtmlUrl = input.RepoHtmlUrl;
	}
}
public class VerseCountingBook
{
	public string BookId { get; set; }
	public List<VerseCountingChapter> Chapters { get; set; } = new();
}
public class VerseCountingChapter
{
	public int ChapterNumber { get; set; }
	public int VerseCount { get; set; }
	public int BlankVerseCount { get; set; }
}
