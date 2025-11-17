namespace PipelineCommon.Models.BusMessages;

/// <summary>
/// Message containing the results of repository analysis
/// </summary>
public class RepoAnalysisResult
{
	/// <summary>
	/// Whether the analysis was successful
	/// </summary>
	public bool Success { get; set; }
	
	/// <summary>
	/// Error message if analysis failed
	/// </summary>
	public string Message { get; set; }
	
	/// <summary>
	/// Repository owner username
	/// </summary>
	public string User { get; set; }
	
	/// <summary>
	/// Repository name
	/// </summary>
	public string Repo { get; set; }
	
	/// <summary>
	/// Repository ID
	/// </summary>
	public int RepoId { get; set; }
	
	/// <summary>
	/// Type of repository (Bible, translationNotes, etc.)
	/// </summary>
	public string RepoType { get; set; }
	
	/// <summary>
	/// Language code (e.g., "en", "es")
	/// </summary>
	public string LanguageCode { get; set; }
	
	/// <summary>
	/// Language name (e.g., "English", "Spanish")
	/// </summary>
	public string LanguageName { get; set; }
	
	/// <summary>
	/// Whether this is a BTTWriter project
	/// </summary>
	public bool IsBTTWriterProject { get; set; }

	public RepoAnalysisResult()
	{
	}

	public RepoAnalysisResult(WACSMessage source)
	{
		User = source.User;
		Repo = source.Repo;
		RepoId = source.RepoId;
	}
}
