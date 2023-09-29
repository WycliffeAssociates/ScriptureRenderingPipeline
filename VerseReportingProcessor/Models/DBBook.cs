namespace VerseReportingProcessor.Models;

public class DBBook
{
    public string Slug { get; set; }
    public int ExpectedChapters { get; set; }
    public int ActualChapters { get; set; }
    public List<DBChapter> Chapters { get; set; } = new();
}