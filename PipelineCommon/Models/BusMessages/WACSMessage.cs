namespace PipelineCommon.Models.BusMessages;

public class WACSMessage
{
    public string EventType { get; set; }
    public string RepoHtmlUrl { get; set; }
    public string User { get; set; }
    public string Repo { get; set; }
    public string LanguageCode { get; set; }
    public string LanguageName { get; set; }
    public string ResourceCode { get; set; }
    public SimplifiedCommit LatestCommit { get; set; }
    public int RepoId { get; set; }
    public string Action { get; set; }
}