namespace VerseReportingProcessor.Models;

public class ComputedResultBook
{
    public string Slug { get; set; }
    public int ExpectedChapters { get; set; }
    public int ActualChapters { get; set; }
    public bool IsEmpty { get; set; }
    public List<ComputedResultChapter> Chapters { get; set; } = new();
}