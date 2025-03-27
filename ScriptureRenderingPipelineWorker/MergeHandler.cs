using System.Net;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using BTTWriterLib;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using Microsoft.Extensions.Configuration;
using PipelineCommon.Helpers;
using PipelineCommon.Models.BusMessages;
using USFMToolsSharp;
using USFMToolsSharp.Models.Markers;
using USFMToolsSharp.Renderers.USFM;

namespace ScriptureRenderingPipelineWorker;

public class MergeTrigger
{
    private readonly ILogger<ProgressReporting> _log;
    private readonly ServiceBusClient _client;
    private readonly GiteaClient _giteaClient;
    private readonly string _destinationUser;
    private readonly string _giteaBaseAddress;
    public MergeTrigger(ILogger<ProgressReporting> logger, IAzureClientFactory<ServiceBusClient> serviceBusClientFactory, IConfiguration config)
    {
        _log = logger;
        _client = serviceBusClientFactory.CreateClient("ServiceBusClient");
        _giteaBaseAddress = config["GiteaBaseAddress"];
        var user = config["GiteaUser"];
        var password = config["GiteaPassword"];
        _destinationUser = config["MergeDestinationUser"];
        _giteaClient = new GiteaClient(_giteaBaseAddress, user, password);
    }
    

    [Function("MergeTrigger")]
    [ServiceBusOutput("MergedResult", Connection = "ServiceBusConnectionString")]
    public async Task<MergeResult> MergeRepos([ServiceBusTrigger("MergeRequest", "MergeRequest", IsSessionsEnabled = false, Connection = "ServiceBusConnectionString")] string messageText)
    {
        var message = JsonSerializer.Deserialize(messageText, WorkerJsonContext.Default.MergeRequest);
        if (message == null)
        {
            _log.LogError("Got invalid message");
            return new MergeResult(false, "", null);
        }
        _log.LogInformation("Got merge request triggered by {User} for {Count} repos", message.RequestingUserName, message.ReposToMerge.Length);
        return await MergeReposAsync(message);
    }
    private async Task<MergeResult> MergeReposAsync(MergeRequest message)
    {
	    var output = new Dictionary<string, string>(message.ReposToMerge.Length);
		var renderer = new USFMRenderer();
		var languageCodes = new HashSet<string>();
		var mergedPORTRepoIds = new List<Guid>();
        // Load all the repos
        foreach (var repo in message.ReposToMerge)
        {
	        var info = await Utils.GetGiteaRepoInformation(repo.HtmlUrl, repo.User, repo.Repo);
	        var projectZip = await GetProjectAsync(repo.HtmlUrl, repo.User, repo.Repo, info.default_branch, _log);
	        if (projectZip == null)
	        {
		        _log.LogError("Unable to load repo {User}/{Repo}", repo.User, repo.Repo);
		        continue;
	        }
	        var basePath = projectZip.GetFolders().FirstOrDefault();
	        var repoInformation = await Utils.GetRepoInformation(_log, projectZip, basePath, repo.Repo);
	        languageCodes.Add(repoInformation.languageCode);
			_log.LogInformation("Merging {User}/{Repo}", repo.User, repo.Repo);
	        if (repoInformation.isBTTWriterProject)
	        {
		        _log.LogDebug("Merging BTT Writer project");
		        MergeWriterProject(projectZip, basePath, repo, output, renderer);
	        }
	        else
	        {
		        _log.LogDebug("Merging USFM project");
		        await MergeUSFMProject(projectZip, output);
	        }
	        mergedPORTRepoIds.Add(repo.RepoPortId);
        }

        if (languageCodes.Count > 1)
		{
	        _log.LogWarning("Multiple languages detected in merge request");
	        return new MergeResult(false, "Multiple languages detected in merge request", message.RequestingUserName);
		}
        var repoName = $"merged-{languageCodes.First()}";
        _log.LogInformation("Uploading into {User}/{Repo}", _destinationUser, repoName);
        
        var existingRepo = await _giteaClient.GetRepository(_destinationUser, repoName);
        if (existingRepo != null)
		{
			_log.LogWarning("Repository already exists");
	        return new MergeResult(false, "Repository we would have merged into already exists", message.RequestingUserName);
		}
        
        var createdRepoId = await UploadContent(_destinationUser, repoName, output);
        return new MergeResult(true, $"{_giteaBaseAddress}/{_destinationUser}/{repoName}", message.RequestingUserName,
	        languageCodes.First(), _destinationUser, repoName, createdRepoId.ToString(), mergedPORTRepoIds);
    }

    private static async Task MergeUSFMProject(ZipFileSystem projectZip, Dictionary<string, string> output)
    {
	    foreach (var file in projectZip.GetAllFiles(".usfm"))
	    {
		    var content = await projectZip.ReadAllTextAsync(file);
		    var fileName = Path.GetFileName(file);
		    output.TryAdd(fileName, content);
	    }
    }

    private void MergeWriterProject(ZipFileSystem projectZip, string? basePath, MergeRequestRepo repo, Dictionary<string, string> output,
	    USFMRenderer renderer)
    {
	    var container = new ZipFileSystemBTTWriterLoader(projectZip,basePath);
	    var usfmObject = BTTWriterLoader.CreateUSFMDocumentFromContainer(container, false, new USFMParser(ignoreUnknownMarkers: true));
	    var bookCode = usfmObject.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation;
				
	    if (bookCode == null)
	    {
		    _log.LogWarning("No book code found for {User}/{Repo}", repo.User, repo.Repo);
		    return;
	    }

	    if (output.ContainsKey(bookCode))
	    {
		    return;
	    }
				
	    var usfm = renderer.Render(usfmObject);
	    output.Add($"{bookCode}.usfm", usfm);
    }

    private async Task<int> UploadContent(string user, string repoName, Dictionary<string,string> content)
    {
		var createdRepo = await _giteaClient.CreateRepository(user, repoName);
		await _giteaClient.UploadMultipleFiles(user,repoName,content);
		return createdRepo!.Id;
    }

    private static async Task<ZipFileSystem?> GetProjectAsync(string repoHtmlUrl, string user, string repo, string defaultBranch, ILogger log)
    {
	    var result = await Utils.httpClient.GetAsync(Utils.GenerateDownloadLink(repoHtmlUrl, user, repo, defaultBranch));
	    if (result.StatusCode == HttpStatusCode.NotFound)
	    {
		    log.LogWarning("Repository at {RepositoryUrl} is empty", repoHtmlUrl);
		    return null;
	    }

	    if (!result.IsSuccessStatusCode)
	    {
		    log.LogError("Error downloading {RepositoryUrl} status code: {StatusCode}", repoHtmlUrl, result.StatusCode);
	    }
	    var zipStream = await result.Content.ReadAsStreamAsync();
	    return new ZipFileSystem(zipStream);
    }
}