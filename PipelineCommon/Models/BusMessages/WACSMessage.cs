namespace PipelineCommon.Models.BusMessages;

public class WACSMessage
{
    public string EventType { get; set; }
    public string RepoHtmlUrl { get; set; }
    public string User { get; set; }
    public string Repo { get; set; }
    public SimplifiedCommit LatestCommit { get; set; }
    public int RepoId { get; set; }
    public string Action { get; set; }
}