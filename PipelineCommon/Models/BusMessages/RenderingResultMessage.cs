namespace PipelineCommon.Models.BusMessages;

public class RenderingResultMessage
{
	public bool Successful { get; set; }
	public string Message { get; set; }
	public string User { get; set; }
	public string Repo { get; set; }
	public int RepoId { get; set; }

	public RenderingResultMessage()
	{

	}

	public RenderingResultMessage(WACSMessage source)
	{
		User = source.User;
		Repo = source.Repo;
		RepoId = source.RepoId;
	}
}
