using System;

namespace PipelineCommon.Models.BusMessages;

public class RenderingResultMessage
{
	public bool Successful { get; set; }
	public string Message { get; set; }
	public string User { get; set; }
	public string Repo { get; set; }
	
	public string RepoUrl { get; set; }
	public string LanguageCode { get; set; }
	public string LanguageName { get; set; }
	public string ResourceType { get; set; }
	public DateTime RenderedAt { get; set; }
	
	public int RepoId { get; set; }

	public RenderingResultMessage(WACSMessage source)
	{
		User = source.User;
		Repo = source.Repo;
		RepoId = source.RepoId;
	}
}
