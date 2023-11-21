using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using DotLiquid;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Logging;
using PipelineCommon.Helpers;
using PipelineCommon.Models.BusMessages;
using ScriptureRenderingPipeline.Models;
using ScriptureRenderingPipeline.Renderers;
using System.Text.Json;

namespace ScriptureRenderingPipeline;

public static class RenderingTrigger
{
    [FunctionName("RenderingTrigger")]
    [ServiceBusAccount("ServiceBusConnectionString")]
    [return: ServiceBus("RepoRendered", ServiceBusEntityType.Topic)]
    public static async Task<ServiceBusMessage> RunAsync([ServiceBusTrigger("WACSEvent", "RenderingWebhook", Connection = "ServiceBusConnectionString")] string rawMessage,
        ILogger log)
    {
	    var message = JsonSerializer.Deserialize<WACSMessage>(rawMessage);
	    var repoRenderResult = await RenderRepoAsync(message, log);
	    var output = new ServiceBusMessage(JsonSerializer.Serialize(repoRenderResult));
	    output.ApplicationProperties["Success"] = repoRenderResult.Successful;
	    return output;
    }


    private static async Task<ZipFileSystem?> GetProjectAsync(WACSMessage message, ILogger log)
    {
	    using var httpClient = new HttpClient();
	    var result = await httpClient.GetAsync(Utils.GenerateDownloadLink(message.RepoHtmlUrl, message.User, message.Repo));
	    if (result.StatusCode == HttpStatusCode.NotFound)
	    {
		    log.LogWarning("Repository at {RepositoryUrl} is empty", message.RepoHtmlUrl);
		    return null;
	    }

	    if (!result.IsSuccessStatusCode)
	    {
		    log.LogError("Error downloading {RepositoryUrl} status code: {StatusCode}", message.RepoHtmlUrl, result.StatusCode);
	    }
	    var zipStream = await result.Content.ReadAsStreamAsync();
	    return new ZipFileSystem(zipStream);
    }

    private static async Task<AppMeta> GetAppMetaAsync(IZipFileSystem fileSystem, string basePath, ILogger log)
    {
		if (fileSystem.FileExists(fileSystem.Join(basePath, ".apps/scripture-rendering-pipeline/meta.json")))
		{
			var jsonMeta =
				await fileSystem.ReadAllTextAsync(fileSystem.Join(basePath,
					".apps/scripture-rendering-pipeline/meta.json"));
			try
			{
				return JsonSerializer.Deserialize<AppMeta>(jsonMeta);
			}
			catch (JsonException)
			{
				log.LogError("invalid json in the apps directory");
			}
		}

		return null;
    }

    private static async Task<RenderingResultMessage> RenderRepoAsync(WACSMessage message, ILogger log)
    {
	    var timeStarted = DateTime.Now;
	    
        log.LogInformation("Rendering {Username}/{Repo}", message.User, message.Repo);
	    var connectionString = Environment.GetEnvironmentVariable("ScripturePipelineStorageConnectionString");
	    var outputContainer = Environment.GetEnvironmentVariable("ScripturePipelineStorageOutputContainer");
	    var templateContainer = Environment.GetEnvironmentVariable("ScripturePipelineStorageTemplateContainer");

	    var outputDir = new FileSystemOutputInterface(Utils.CreateTempFolder());

	    var rendererInput = new RendererInput()
	    {
		    BaseUrl = Environment.GetEnvironmentVariable("ScriptureRenderingPipelineBaseUrl"),
		    UserToRouteResourcesTo = Environment.GetEnvironmentVariable("ScriptureRenderingPipelineResourcesUser"),
	    };


	    var downloadPrintPageTemplateTask = GetTemplateAsync(connectionString, templateContainer, "print.html");

	    log.LogInformation($"Downloading repo");
	    rendererInput.FileSystem = await GetProjectAsync(message, log);
	    
	    if (rendererInput.FileSystem == null)
	    {
	        log.LogWarning("Repo not found or is empty");
		    return new RenderingResultMessage(message)
		    {
			    Successful = false,
			    Message = "Can't download source zip, probably an empty repo",
		    };
	    }

	    rendererInput.BasePath = rendererInput.FileSystem.GetFolders().FirstOrDefault();


	    var repoType = RepoType.Unknown;
	    string exceptionMessage = null;
	    var title = string.Empty;
	    string template = null;
	    var converterUsed = string.Empty;
	    try
	    {
		    // Determine type of repo
		    var repoInformation = await Utils.GetRepoInformation(log, rendererInput.FileSystem, rendererInput.BasePath, message.Repo);
		    rendererInput.ResourceContainer = repoInformation.ResourceContainer;
		    rendererInput.IsBTTWriterProject = repoInformation.isBTTWriterProject;
		    rendererInput.LanguageCode = repoInformation.languageCode;
		    rendererInput.LanguageTextDirection = repoInformation.languageDirection;
		    rendererInput.ResourceName = repoInformation.resourceName;
		    repoType = repoInformation.repoType;

		    
		    if (repoType == RepoType.Unknown)
		    {
			    return new RenderingResultMessage(message)
			    {
				    Successful = false,
				    Message = "Unable to determine type of repo"
			    };
		    }

		    rendererInput.AppsMeta = await GetAppMetaAsync(rendererInput.FileSystem, rendererInput.BasePath, log);

		    rendererInput.Title = BuildDisplayName(repoInformation.languageName, repoInformation.resourceName);

		    log.LogInformation("Starting render");
		    rendererInput.PrintTemplate = Template.Parse(await downloadPrintPageTemplateTask);
			converterUsed = BuildConverterName(repoType, rendererInput.IsBTTWriterProject);
			
		    await RenderContentAsync(log, repoType, rendererInput, outputDir);
	    }
	    catch (Exception e)
	    {
		    log.LogError(e, e.Message);
		    exceptionMessage = e.Message;
	    }

	    // Create the build_log.json
	    var buildLog = new BuildLog()
	    {
		    success = string.IsNullOrEmpty(exceptionMessage),
		    ended_at = DateTime.Now,
		    created_at = timeStarted,
		    started_at = timeStarted,
		    convert_module = converterUsed,
		    lint_module = null,
		    status = string.IsNullOrEmpty(exceptionMessage) ? "success" : "failure",
		    repo_name = message.Repo,
		    repo_owner = message.User,
		    message = string.IsNullOrEmpty(exceptionMessage) ? "Conversion successful" : "Conversion failed",
		    warnings = Array.Empty<string>(),
	    };
	    if (message.LatestCommit != null)
	    {
		    buildLog.commit_message = message.LatestCommit.Message;
		    buildLog.committed_by = message.LatestCommit.Username;
		    buildLog.commit_url = message.LatestCommit.Url;
		    buildLog.commit_id = message.LatestCommit.Hash;
	    }
	    else
	    {
		    log.LogWarning(
			    "There were no commits in the push so not the information in the build_log.json won't have this");
	    }

	    // Write build log
	    await outputDir.WriteAllTextAsync("build_log.json", JsonSerializer.Serialize(buildLog));
	    
	    await OutputErrorIfPresentAsync(exceptionMessage, template, outputDir);

	    log.LogInformation("Starting upload");
	    await Utils.UploadToStorage(log, connectionString, outputContainer, outputDir, $"/u/{message.User}/{message.Repo}");

	    rendererInput.FileSystem.Close();
	    log.LogInformation("Cleaning up temporary files");

	    // Clean up output dir
	    outputDir.Dispose();

	    if (!string.IsNullOrEmpty(exceptionMessage))
	    {
		    return new RenderingResultMessage(message)
		    {
			    Successful = false,
			    Message = exceptionMessage
		    };
	    }

	    return new RenderingResultMessage(message)
	    {
		    Successful = true
	    };
    }

    private static async Task RenderContentAsync(ILogger log, RepoType repoType, RendererInput rendererInput, IOutputInterface outputDir)
    {
	    if (repoType != RepoType.Bible && repoType != RepoType.BibleCommentary)
	    {
		    if (rendererInput.ResourceContainer == null)
		    {
			    throw new Exception("Can't render without a manifest.");
		    }
	    }

	    var renderer = SelectRenderer(log, repoType, rendererInput);

	    await renderer.RenderAsync(rendererInput, outputDir);
    }

    private static IRenderer SelectRenderer(ILogger log, RepoType repoType, RendererInput rendererInput)
    {
	    IRenderer renderer = null;
	    switch (repoType)
	    {
		    case RepoType.Bible:
			    log.LogInformation("Rendering Bible");
			    renderer = new BibleRenderer();
			    break;
		    case RepoType.translationNotes:
			    log.LogInformation("Rendering translationNotes");
			    renderer = new TranslationNotesRenderer();
			    break;
		    case RepoType.translationQuestions:
			    log.LogInformation("Rendering translationQuestions");
			    renderer = new TranslationQuestionsRenderer();
			    break;
		    case RepoType.translationWords:
			    log.LogInformation("Rendering translationWords");
			    renderer = new TranslationWordsRenderer();
			    break;
		    case RepoType.translationAcademy:
			    log.LogInformation("Rendering translationManual");
			    renderer = new TranslationManualRenderer();
			    break;
		    case RepoType.BibleCommentary:
			    log.LogInformation("Rendering Bible Commentary");
			    renderer = new CommentaryRenderer();
			    break;
		    default:
			    throw new Exception($"Unable to render type {repoType}");
	    }

	    return renderer;
    }

    private static async Task OutputErrorIfPresentAsync(string exceptionMessage, string template, IOutputInterface outputDir)
    {
	    if (!string.IsNullOrEmpty(exceptionMessage))
	    {
		    var errorPage = "";
		    if (string.IsNullOrEmpty(template))
		    {
			    errorPage = "<h1>Render Error</h1> Unable to load template so falling back to plain html <br/>" +
			                exceptionMessage;
		    }
		    else
		    {
			    errorPage = Template.Parse(template)
				    .Render(Hash.FromAnonymousObject(new { content = "<h1>Render Error</h1> " + exceptionMessage }));
		    }

		    await outputDir.WriteAllTextAsync("index.html", errorPage);
	    }
    }

    private static string BuildConverterName(RepoType repoType, bool isBTTWriterProject)
	{
	    return $"{Enum.GetName(repoType)}.{(isBTTWriterProject ? "BTTWriter" : "Normal")}";
	}


    private static string BuildDisplayName(string language, string resource)
	{
		return $"{language ?? "Unknown"}: {resource ?? "Unknown"}";
	}


	private static async Task<string> GetTemplateAsync(string connectionString, string templateContainer, string templateFile)
	{
		var blobClient = new BlobClient(connectionString, templateContainer, templateFile);
		var templateStream = new MemoryStream();
		await blobClient.DownloadToAsync(templateStream);
		templateStream.Seek(0, SeekOrigin.Begin);
		return await new StreamReader(templateStream).ReadToEndAsync();
	}
}