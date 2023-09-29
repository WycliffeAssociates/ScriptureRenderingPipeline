namespace VerseReportingProcessor.Models;

public abstract class BookCountDefinitions
{
    public int ExpectedChapters { get; set; }
    public Dictionary<int, int> ExpectedChapterCounts { get; set; }
}