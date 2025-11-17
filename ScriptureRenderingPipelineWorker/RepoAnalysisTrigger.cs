using System.Net;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using PipelineCommon.Helpers;
using PipelineCommon.Models.BusMessages;

namespace ScriptureRenderingPipelineWorker;

/// <summary>
/// Azure Function that analyzes repositories from WACS messages
/// </summary>
public class RepoAnalysisTrigger
{
	private readonly ILogger<RepoAnalysisTrigger> log;
	private readonly ServiceBusClient client;

	public RepoAnalysisTrigger(ILogger<RepoAnalysisTrigger> logger, IAzureClientFactory<ServiceBusClient> serviceBusClientFactory)
	{
		log = logger;
		client = serviceBusClientFactory.CreateClient("ServiceBusClient");
	}

	[Function("RepoAnalysisTrigger")]
	public async Task RunAsync([ServiceBusTrigger("WACSEvent", "RepoAnalysis", IsSessionsEnabled = false, Connection = "ServiceBusConnectionString")] string rawMessage)
	{
		var message = JsonSerializer.Deserialize(rawMessage, WorkerJsonContext.Default.WACSMessage);
		var analysisResult = await AnalyzeRepoAsync(message, log);
		var output = new ServiceBusMessage(JsonSerializer.Serialize(analysisResult, WorkerJsonContext.Default.RepoAnalysisResult))
		{
			ApplicationProperties =
			{
				["Success"] = analysisResult.Success
			}
		};
		await using var sender = client.CreateSender("RepoAnalysisResult");
		await sender.SendMessageAsync(output);
	}

	private static async Task<RepoAnalysisResult> AnalyzeRepoAsync(WACSMessage message, ILogger log)
	{
		log.LogInformation("Analyzing repository {Username}/{Repo}", message.User, message.Repo);

		var result = new RepoAnalysisResult(message);

		// Download the repository
		var fileResult = await Utils.httpClient.GetAsync(Utils.GenerateDownloadLink(message.RepoHtmlUrl, message.User, message.Repo, message.DefaultBranch));

		log.LogDebug("Got status code: {StatusCode}", fileResult.StatusCode);

		if (fileResult.StatusCode == HttpStatusCode.NotFound)
		{
			log.LogWarning("Repository not found or is empty");
			result.Success = false;
			result.Message = "Repository not found or is empty";
			return result;
		}

		if (!fileResult.IsSuccessStatusCode)
		{
			log.LogError("Failed to download repository: {StatusCode}", fileResult.StatusCode);
			result.Success = false;
			result.Message = $"Failed to download repository: HTTP {fileResult.StatusCode}";
			return result;
		}

		// Extract and analyze the repository
		try
		{
			var zipStream = await fileResult.Content.ReadAsStreamAsync();
			var fileSystem = new ZipFileSystem(zipStream);
			var basePath = fileSystem.GetFolders().FirstOrDefault();

			if (basePath == null)
			{
				log.LogWarning("Repository appears to be empty");
				result.Success = false;
				result.Message = "Repository appears to be empty";
				return result;
			}

			// Get repository information
			var repoInfo = await Utils.GetRepoInformation(log, fileSystem, basePath, message.Repo);

			// Populate the result
			result.Success = true;
			result.Message = "Repository analyzed successfully";
			result.RepoType = repoInfo.repoType.ToString();
			result.LanguageCode = repoInfo.languageCode;
			result.LanguageName = repoInfo.languageName;
			result.IsBTTWriterProject = repoInfo.isBTTWriterProject;

			fileSystem.Close();

			log.LogInformation("Analysis complete: Type={RepoType}, Language={LanguageCode}, BTTWriter={IsBTTWriter}",
				result.RepoType, result.LanguageCode, result.IsBTTWriterProject);
		}
		catch (Exception ex)
		{
			log.LogError(ex, "Error analyzing repository");
			result.Success = false;
			result.Message = $"Error analyzing repository: {ex.Message}";
		}

		return result;
	}
}
