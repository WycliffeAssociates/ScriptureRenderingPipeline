using System.Net;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using DotLiquid;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using PipelineCommon.Helpers;
using PipelineCommon.Models.BusMessages;
using ScriptureRenderingPipelineWorker.Models;
using ScriptureRenderingPipelineWorker.Renderers;

namespace ScriptureRenderingPipelineWorker;

public class RenderingTrigger
{
	private ILogger<RenderingTrigger> log;
	private readonly ServiceBusClient client;

	public RenderingTrigger(ILogger<RenderingTrigger> logger, IAzureClientFactory<ServiceBusClient> serviceBusClientFactory)
	{
		log = logger;
		client = serviceBusClientFactory.CreateClient("ServiceBusClient");
	}
	
    [Function("RenderingTrigger")]
    public async Task RunAsync([ServiceBusTrigger("WACSEvent", "RenderingWebhook", IsSessionsEnabled = false, Connection = "ServiceBusConnectionString")] string rawMessage )
    {
	    var message = JsonSerializer.Deserialize(rawMessage, WorkerJsonContext.Default.WACSMessage);
	    var repoRenderResult = await RenderRepoAsync(message, log);
	    var output = new ServiceBusMessage(JsonSerializer.Serialize(repoRenderResult, WorkerJsonContext.Default.RenderingResultMessage))
		    {
			    ApplicationProperties =
			    {
				    ["Success"] = repoRenderResult.Successful
			    }
		    };
	    await using var sender = client.CreateSender("RepoRendered");
	    await sender.SendMessageAsync(output);
    }


    private static async Task<ZipFileSystem?> GetProjectAsync(WACSMessage message, ILogger log)
    {
	    var result = await Utils.httpClient.GetAsync(Utils.GenerateDownloadLink(message.RepoHtmlUrl, message.User, message.Repo, message.DefaultBranch));
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

    private static async Task<AppMeta?> GetAppMetaAsync(IZipFileSystem fileSystem, string basePath, ILogger log)
    {
		if (fileSystem.FileExists(fileSystem.Join(basePath, ".apps/scripture-rendering-pipeline/meta.json")))
		{
			var jsonMeta =
				await fileSystem.ReadAllTextAsync(fileSystem.Join(basePath,
					".apps/scripture-rendering-pipeline/meta.json"));
			try
			{
				return JsonSerializer.Deserialize(jsonMeta, WorkerJsonContext.Default.AppMeta);
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

	    var outputDir = new DirectAzureUpload($"/u/{message.User}/{message.Repo}", Utils.GetOutputClient());

	    var rendererInput = new RendererInput()
	    {
		    BaseUrl = Environment.GetEnvironmentVariable("ScriptureRenderingPipelineBaseUrl"),
		    UserToRouteResourcesTo = Environment.GetEnvironmentVariable("ScriptureRenderingPipelineResourcesUser"),
				RepoUrl = message.RepoHtmlUrl
	    };


	    var downloadPrintPageTemplateTask = GetTemplateAsync("print.html");

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
	    FileTrackingLogger fileTracker = null;
	    try
	    {
		    // Determine type of repo
		    var repoInformation =
			    await Utils.GetRepoInformation(log, rendererInput.FileSystem, rendererInput.BasePath, message.Repo);
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
			fileTracker = new FileTrackingLogger($"{rendererInput.BaseUrl.TrimEnd('/')}/u/{message.User}/{message.Repo}", repoType);
			rendererInput.Logger = fileTracker;

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
	    await outputDir.WriteAllTextAsync("build_log.json", JsonSerializer.Serialize(buildLog, WorkerJsonContext.Default.BuildLog));
	    
	    await OutputErrorIfPresentAsync(exceptionMessage, template, outputDir);

	    log.LogInformation("Starting upload");
	    await outputDir.FinishAsync();

	    rendererInput.FileSystem.Close();

	    if (!string.IsNullOrEmpty(exceptionMessage))
	    {
		    return new RenderingResultMessage(message)
		    {
			    Successful = false,
			    Message = Truncate(exceptionMessage, 100_000),
			    RenderedAt = timeStarted
		    };
	    }

	    return CreateSuccessfulResultMessage(message, timeStarted, rendererInput, repoType, fileTracker);
    }
    private static string Truncate(string value, int maxLength)
	{
	    if (string.IsNullOrEmpty(value))
	    {
		    return value;
	    }
	    return value.Length <= maxLength ? value : value[..maxLength];
	}

    private static RenderingResultMessage CreateSuccessfulResultMessage(WACSMessage message, DateTime timeStarted,
	    RendererInput rendererInput, RepoType resourceType, FileTrackingLogger fileTracker)
    {
	    return new RenderingResultMessage(message)
	    {
		    Successful = true,
		    RenderedAt = timeStarted,
		    LanguageCode = rendererInput.LanguageCode,
		    LanguageName = rendererInput.LanguageName,
		    ResourceName = rendererInput.ResourceName,
		    RepoUrl = rendererInput.RepoUrl,
		    ResourceType = resourceType switch
		    {
			    RepoType.Bible => "bible",
			    RepoType.translationAcademy => "tm",
			    RepoType.translationNotes => "tn",
			    RepoType.translationQuestions => "tq",
			    RepoType.translationWords => "tw",
			    RepoType.BibleCommentary => "bc",
			    _ => "unknown"
		    },
		    RenderedFiles = fileTracker?.Files,
				FileBasePath = fileTracker?.BaseUrl,
		    Titles = fileTracker?.Titles
	    };
    }

    private static async Task RenderContentAsync(ILogger log, RepoType repoType, RendererInput rendererInput, IOutputInterface outputDir)
    {
	    if (repoType != RepoType.Bible && repoType != RepoType.BibleCommentary)
	    {
		    if (rendererInput.ResourceContainer == null)
		    {
			    throw new UnrenderableException("Can't render without a manifest.");
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
			    throw new UnrenderableException($"Unable to render type {repoType}");
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


	private static async Task<string> GetTemplateAsync(string templateFile)
	{
		var blobClient = Utils.GetTemplateClient().GetBlobClient(templateFile);
		
		var templateStream = new MemoryStream();
		await blobClient.DownloadToAsync(templateStream);
		templateStream.Seek(0, SeekOrigin.Begin);
		return await new StreamReader(templateStream).ReadToEndAsync();
	}
}