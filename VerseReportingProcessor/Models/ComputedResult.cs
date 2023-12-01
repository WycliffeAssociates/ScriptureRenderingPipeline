namespace VerseReportingProcessor.Models;

public class ComputedResult
{
    public string? LanguageCode { get; set; }
    public string? Repo { get; set; }
    public string? User { get; set; }
    public int? RepoId { get; set; }
    public List<ComputedResultBook> Books { get; set; } = new ();
}