namespace VerseReportingProcessor.Models;

public class DBModel
{
    public string LanguageCode { get; set; }
    public string Repo { get; set; }
    public string User { get; set; }
    public int? RepoId { get; set; }
    public List<DBBook> Books { get; set; } = new ();

    public DBModel()
    {
        
    }

}