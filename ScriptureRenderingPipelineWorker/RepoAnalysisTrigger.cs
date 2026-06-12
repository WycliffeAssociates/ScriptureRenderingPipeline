using System.Net;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
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
	private readonly int _maxRepoSizeInMB;

	public RepoAnalysisTrigger(ILogger<RepoAnalysisTrigger> logger, IAzureClientFactory<ServiceBusClient> serviceBusClientFactory, IConfiguration configuration)
	{
		log = logger;
		client = serviceBusClientFactory.CreateClient("ServiceBusClient");
		_maxRepoSizeInMB = configuration.GetValue("MaxRepoSizeInMB", 0);
	}

	[Function("RepoAnalysisTrigger")]
	public async Task RunAsync([ServiceBusTrigger("WACSEvent", "RepoAnalysis", IsSessionsEnabled = false, Connection = "ServiceBusConnectionString")] string rawMessage)
	{
		var message = JsonSerializer.Deserialize(rawMessage, WorkerJsonContext.Default.WACSMessage);
		var analysisResult = await AnalyzeRepoAsync(message, log, _maxRepoSizeInMB);
		var output = new ServiceBusMessage(JsonSerializer.Serialize(analysisResult, WorkerJsonContext.Default.RepoAnalysisResult))
		{
			ApplicationProperties =
			{
				["Success"] = analysisResult.Success,
				["Action"] = analysisResult.Action,
				["EventType"] = analysisResult.EventType,
				["RepoType"] = analysisResult.RepoType
			}
		};
		await using var sender = client.CreateSender("RepoAnalysisResult");
		await sender.SendMessageAsync(output);
	}

	private static async Task<RepoAnalysisResult> AnalyzeRepoAsync(WACSMessage message, ILogger log, int maxRepoSizeInMB)
	{
		log.LogInformation("Analyzing repository {Username}/{Repo}", message.User, message.Repo);

		var result = new RepoAnalysisResult(message);

		if (Utils.IsRepoTooLarge(message.RepoSizeInKB, maxRepoSizeInMB))
		{
			log.LogWarning("Skipping {Username}/{Repo}: repository size {Size}KB exceeds the limit of {Limit}MB",
				message.User, message.Repo, message.RepoSizeInKB, maxRepoSizeInMB);
			result.Success = false;
			result.Message = $"Repository size {message.RepoSizeInKB}KB exceeds the limit of {maxRepoSizeInMB}MB, skipping";
			return result;
		}

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
			result.LanguageDirection = repoInfo.languageDirection;
			result.ResourceName = repoInfo.resourceName;
			result.ResourceId = repoInfo.ResourceContainer?.dublin_core?.identifier;
			result.RepoFomat = repoInfo.RepoFormat.ToString();

			fileSystem.Close();

			log.LogInformation("Analysis complete: Type={RepoType}, Language={LanguageCode}, RepoFormat={RepoFormat}",
				result.RepoType, result.LanguageCode, result.RepoFomat);
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
