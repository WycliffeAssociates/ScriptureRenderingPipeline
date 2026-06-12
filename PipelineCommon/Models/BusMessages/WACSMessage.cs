namespace PipelineCommon.Models.BusMessages;

public class WACSMessage
{
    public string EventType { get; set; }
    public string RepoHtmlUrl { get; set; }
    public string User { get; set; }
    public string Repo { get; set; }
    public SimplifiedCommit LatestCommit { get; set; }
    public string LastCommitId { get; set; }
    public int RepoId { get; set; }
    public string Action { get; set; }
    public string DefaultBranch { get; set; }
    public string[] Topics { get; set; }
    /// <summary>
    /// Repository size in kibibytes, as reported by Gitea in the webhook payload.
    /// </summary>
    public int RepoSizeInKB { get; set; }
}