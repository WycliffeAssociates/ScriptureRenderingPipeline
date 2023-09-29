namespace VerseReportingProcessor.Models;

public class CountDefinitions
{
    public Dictionary<string, BookCountDefinitions> Books { get; set; } = new();
}