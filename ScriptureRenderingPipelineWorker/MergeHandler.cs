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
        var result = await MergeReposAsync(message);
        return new MergeResult(result.Item1, result.Item2, message.RequestingUserName);
    }
    private async Task<(bool,string)> MergeReposAsync(MergeRequest message)
    {
	    var output = new Dictionary<string, string>(message.ReposToMerge.Length);
		var renderer = new USFMRenderer();
		var languageCodes = new HashSet<string>();
        // Load all the repos
        foreach (var repo in message.ReposToMerge)
        {
	        var info = await Utils.GetGiteaRepoInformation(repo.HtmlUrl, repo.User, repo.Repo);
	        var projectZip = await GetProjectAsync(repo.HtmlUrl, repo.User, repo.Repo, info.default_branch, _log);
	        var basePath = projectZip.GetFolders().FirstOrDefault();
	        var repoInformation = await Utils.GetRepoInformation(_log, projectZip, basePath, repo.Repo);
	        languageCodes.Add(repoInformation.languageCode);
	        var container = new ZipFileSystemBTTWriterLoader(projectZip,basePath);
	        var usfmObject = BTTWriterLoader.CreateUSFMDocumentFromContainer(container, false, new USFMParser(ignoreUnknownMarkers: true));
	        var bookCode = usfmObject.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation;
	        
	        if (bookCode == null)
	        {
		        _log.LogWarning("No book code found for {User}/{Repo}", repo.User, repo.Repo);
		        continue;
	        }

	        if (output.ContainsKey(bookCode))
	        {
		        continue;
	        }
	        
	        var usfm = renderer.Render(usfmObject);
	        output.Add($"{bookCode}.usfm", usfm);
            _log.LogInformation("Merging {User}/{Repo}", repo.User, repo.Repo);
        }

        if (languageCodes.Count > 1)
		{
	        _log.LogWarning("Multiple languages detected in merge request");
	        return (false,"Multiple languages detected");
		}
        var repoName = $"merged-{languageCodes.First()}";
        _log.LogInformation("Uploading into {User}/{Repo}", _destinationUser, repoName);

        await UploadContent(_destinationUser, repoName, output);
        return (true, $"{_giteaBaseAddress}/{_destinationUser}/{repoName}");
    }

    private async Task UploadContent(string user, string repoName, Dictionary<string,string> content)
    {
	    var existingRepo = await _giteaClient.GetRepository(user, repoName);
	    if (existingRepo == null)
	    {
		    await _giteaClient.CreateRepository(user, repoName);
		    await _giteaClient.UploadMultipleFiles(user,repoName,content);
	    }
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