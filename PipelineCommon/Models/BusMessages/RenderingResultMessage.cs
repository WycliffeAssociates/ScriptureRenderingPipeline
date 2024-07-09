using System;
using System.Collections.Generic;

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
	public List<RenderedFile> RenderedFiles { get; set; } = new List<RenderedFile>();
	public string FileBasePath { get; set; }
	
	public Dictionary<string,string> Titles { get; set; } = new Dictionary<string, string>();

	public RenderingResultMessage(WACSMessage source)
	{
		User = source.User;
		Repo = source.Repo;
		RepoId = source.RepoId;
	}
}
