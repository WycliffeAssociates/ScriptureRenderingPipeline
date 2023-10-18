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

	public VerseCountingResult()
	{
		
	}

	public VerseCountingResult(WACSMessage input)
	{
		User = input.User;
		Repo = input.Repo;
		RepoId = input.RepoId;
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
}
