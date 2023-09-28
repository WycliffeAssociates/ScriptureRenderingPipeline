using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using BTTWriterLib;
using BTTWriterLib.Models;
using DotLiquid;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;
using ScriptureRenderingPipeline.Models;
using ScriptureRenderingPipeline.Renderers;
using YamlDotNet.Serialization;
using JsonSerializer = System.Text.Json.JsonSerializer;

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

    private static async Task<RenderingResultMessage> RenderRepoAsync(WACSMessage message, ILogger log)
    {
	    var connectionString = Environment.GetEnvironmentVariable("ScripturePipelineStorageConnectionString");
	    var outputContainer = Environment.GetEnvironmentVariable("ScripturePipelineStorageOutputContainer");
	    var templateContainer = Environment.GetEnvironmentVariable("ScripturePipelineStorageTemplateContainer");
	    var baseUrl = Environment.GetEnvironmentVariable("ScriptureRenderingPipelineBaseUrl");
	    var userToRouteResourcesTo = Environment.GetEnvironmentVariable("ScriptureRenderingPipelineResourcesUser");

	    var timeStarted = DateTime.Now;

	    using var httpClient = new HttpClient();

	    var downloadPrintPageTemplateTask = GetTemplateAsync(connectionString, templateContainer, "print.html");

	    log.LogInformation($"Downloading repo");
	    var result = await httpClient.GetAsync($"{message.RepoHtmlUrl}/archive/master.zip");
	    if (result.StatusCode == HttpStatusCode.NotFound)
	    {
		    log.LogInformation("Can't download source zip, probably an empty repo");
		    return new RenderingResultMessage(message)
		    {
			    Successful = false,
			    Message = "Can't download source zip, probably an empty repo",
		    };
	    }

	    var zipStream = await result.Content.ReadAsStreamAsync();
	    var fileSystem = new ZipFileSystem(zipStream);

	    var repoType = RepoType.Unknown;
	    var isBTTWriterProject = false;
	    var outputDir = Utils.CreateTempFolder();
	    string exceptionMessage = null;
	    var title = string.Empty;
	    string template = null;
	    var converterUsed = string.Empty;
	    var resourceName = string.Empty;
	    var languageDirection = "ltr";
	    var languageCode = string.Empty;
	    AppMeta appsMeta = null;
	    try
	    {
		    var basePath = fileSystem.GetFolders().FirstOrDefault();
		    // Determine type of repo
		    ResourceContainer resourceContainer = null;

		    var repoInformation = await Utils.GetRepoInformation(log, fileSystem, basePath, message.Repo);
		    resourceContainer = repoInformation.ResourceContainer;
		    isBTTWriterProject = repoInformation.isBTTWriterProject;
		    languageCode = repoInformation.languageCode;
		    languageDirection = repoInformation.languageDirection;
		    resourceName = repoInformation.resourceName;
		    repoType = repoInformation.repoType;


		    if (repoType == RepoType.Unknown)
		    {
			    throw new Exception("Unable to determine type of repo");
		    }

		    if (fileSystem.FileExists(fileSystem.Join(basePath, ".apps/scripture-rendering-pipeline/meta.json")))
		    {
			    var jsonMeta =
				    await fileSystem.ReadAllTextAsync(fileSystem.Join(basePath,
					    ".apps/scripture-rendering-pipeline/meta.json"));
			    try
			    {
				    appsMeta = JsonSerializer.Deserialize<AppMeta>(jsonMeta);
			    }
			    catch (System.Text.Json.JsonException)
			    {
				    log.LogError("invalid json in the apps directory");
			    }
		    }

		    title = BuildDisplayName(repoInformation.languageName, resourceName);

		    log.LogInformation("Starting render");
		    var printTemplate = await downloadPrintPageTemplateTask;
		    switch (repoType)
		    {
			    case RepoType.Bible:
				    converterUsed = isBTTWriterProject ? "Bible.BTTWriter" : "Bible.Normal";
				    log.LogInformation("Rendering Bible");
				    await BibleRenderer.RenderAsync(fileSystem, basePath, outputDir, Template.Parse(printTemplate),
					    message.RepoHtmlUrl, title, languageCode, repoInformation.languageName, languageDirection,
					    isBTTWriterProject, appsMeta);
				    break;
			    case RepoType.translationNotes:
				    if (resourceContainer == null)
				    {
					    throw new Exception("Can't render translationNotes without a manifest.");
				    }

				    converterUsed = isBTTWriterProject ? "translationNotes.BTTWriter" : "translationNotes.Normal";
				    log.LogInformation("Rendering translationNotes");
				    await new TranslationNotesRenderer().RenderAsync(fileSystem, basePath, outputDir,
					    Template.Parse(printTemplate), message.RepoHtmlUrl, title, baseUrl, userToRouteResourcesTo,
					    languageDirection, languageCode, repoInformation.languageName, isBTTWriterProject, appsMeta);
				    break;
			    case RepoType.translationQuestions:
				    if (resourceContainer == null)
				    {
					    throw new Exception("Can't render translationQuestions without a manifest.");
				    }

				    converterUsed = isBTTWriterProject ? "translationQuestions.BTTWriter" : "translationQuestions.Normal";
				    log.LogInformation("Rendering translationQuestions");
				    await new TranslationQuestionsRenderer().RenderAsync(fileSystem, basePath, outputDir,
					    Template.Parse(printTemplate), message.RepoHtmlUrl, title, baseUrl, userToRouteResourcesTo,
					    languageDirection, languageCode, repoInformation.languageName, isBTTWriterProject, appsMeta);
				    break;
			    case RepoType.translationWords:
				    if (resourceContainer == null)
				    {
					    throw new Exception("Can't render translationWords without a manifest.");
				    }

				    converterUsed = isBTTWriterProject ? "translationWords.BTTWriter" : "translationWords.Normal";
				    log.LogInformation("Rendering translationWords");
				    await new TranslationWordsRenderer().RenderAsync(fileSystem, basePath, outputDir,
					    Template.Parse(printTemplate), message.RepoHtmlUrl, title, resourceContainer, baseUrl,
					    userToRouteResourcesTo, languageDirection, languageCode, repoInformation.languageName,
					    isBTTWriterProject, appsMeta);
				    break;
			    case RepoType.translationAcademy:
				    if (resourceContainer == null)
				    {
					    throw new Exception("Can't render translationManual/translationAcademy without a manifest.");
				    }

				    converterUsed = isBTTWriterProject ? "translationManual.BTTWriter" : "translationManual.Normal";
				    log.LogInformation("Rendering translationManual");
				    await new TranslationManualRenderer().RenderAsync(fileSystem, basePath, outputDir,
					    Template.Parse(template), Template.Parse(printTemplate), message.RepoHtmlUrl, title,
					    resourceContainer, baseUrl, userToRouteResourcesTo, languageDirection, languageCode,
					    isBTTWriterProject, appsMeta);
				    break;
			    case RepoType.BibleCommentary:
				    converterUsed = "bibleCommentary.Normal";
				    log.LogInformation("Rendering Bible Commentary");
				    await new CommentaryRenderer().RenderAsync(fileSystem, basePath, outputDir, Template.Parse(template),
					    Template.Parse(printTemplate), message.RepoHtmlUrl, title, resourceContainer, baseUrl,
					    userToRouteResourcesTo, languageDirection, repoInformation.languageName, languageCode,
					    isBTTWriterProject, appsMeta);
				    break;
			    default:
				    throw new Exception($"Unable to render type {repoType}");
		    }
	    }
	    catch (Exception e)
	    {
		    log.LogError(e, e.Message);
		    exceptionMessage = e.Message;
	    }

	    // Create the build_log.json
	    BuildLog buildLog = new BuildLog()
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
	    File.WriteAllText(Path.Join(outputDir, "build_log.json"), JsonConvert.SerializeObject(buildLog));
	    if (!string.IsNullOrEmpty(exceptionMessage))
	    {
		    string errorPage = "";
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

		    File.WriteAllText(Path.Join(outputDir, "index.html"), errorPage);
	    }

	    log.LogInformation("Starting upload");
	    await Utils.UploadToStorage(log, connectionString, outputContainer, outputDir, $"/u/{message.User}/{message.Repo}");

	    fileSystem.Close();
	    log.LogInformation("Cleaning up temporary files");

	    if (Directory.Exists(outputDir))
	    {
		    Directory.Delete(outputDir, true);
	    }

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


public class WACSMessage
{
    public string EventType { get; set; }
    public string RepoHtmlUrl { get; set; }
    public string User { get; set; }
    public string Repo { get; set; }
    public string LanguageCode { get; set; }
    public string LanguageName { get; set; }
    public string ResourceCode { get; set; }
    public SimplifiedCommit LatestCommit { get; set; }
    public int RepoId { get; set; }
    public string Action { get; set; }
}

public class SimplifiedCommit
{
	public string Hash { get; set; }
	public string Message { get; set; }
	public string Url { get; set; }
	public string Username { get; set; }
	public DateTime Timestamp { get; set; }
}

public class RenderingResultMessage
{
	public bool Successful { get; set; }
	public string Message { get; set; }
	public string User { get; set; }
	public string Repo { get; set; }

	public RenderingResultMessage()
	{
		
	}

	public RenderingResultMessage(WACSMessage source)
	{
		User = source.User;
		Repo = source.Repo;
	}
}