using System.Net;
using System.Security.Cryptography;
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
using PipelineCommon.Models.ResourceContainer;
using ScriptureBurrito;
using ScriptureBurrito.Models;
using USFMToolsSharp;
using USFMToolsSharp.Models.Markers;
using USFMToolsSharp.Renderers.USFM;
using YamlDotNet.Serialization;
using Generator = BTTWriterLib.Models.Generator;
using Language = PipelineCommon.Models.ResourceContainer.Language;
using License = System.ComponentModel.License;

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
            return new MergeResult(false, "Something went really wrong, got an invalid message", null);
        }
        _log.LogInformation("Got merge request triggered by {User} for {Count} repos", message.RequestingUserName, message.ReposToMerge.Length);
        return await MergeReposAsync(message);
    }
    
    private async Task<MergeResult> MergeReposAsync(MergeRequest message)
    {
	    var output = new Dictionary<string, string>(message.ReposToMerge.Length);
		var renderer = new USFMRenderer();
		var languageCodes = new HashSet<string>();
		var languageDirection = "ltr";
		var languageName = "";
		var mergedPORTRepoIds = new List<Guid>();
        // Load all the repos
        var projects = new List<Project>();
        var contributors = new List<string>();
        var sources = new List<Source>();
        var contentForBurrito = new List<ContentForBurrito>();
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
	        languageName = repoInformation.languageName;
	        languageDirection = repoInformation.languageDirection;
	        
			_log.LogInformation("Merging {User}/{Repo}", repo.User, repo.Repo);
	        if (repoInformation.isBTTWriterProject)
	        {
		        _log.LogDebug("Merging BTT Writer project");
		        MergeWriterProject(projectZip, basePath, repo, output, renderer, contentForBurrito);
		        var tmpProject = repoInformation.ResourceContainer.projects[0];
		        tmpProject.path = $"./{tmpProject.identifier}.usfm";
		        projects.Add(tmpProject);
	        }
	        else
	        {
		        _log.LogDebug("Merging USFM project");
		        await MergeUSFMProject(projectZip, output, contentForBurrito);
				projects.AddRange(repoInformation.ResourceContainer.projects);
	        }
			contributors.AddRange(repoInformation?.ResourceContainer?.dublin_core?.contributor ?? []);
			sources.AddRange(repoInformation?.ResourceContainer?.dublin_core?.source ?? []);
	        mergedPORTRepoIds.Add(repo.RepoPortId);
        }

        if (languageCodes.Count > 1)
		{
	        _log.LogWarning("Multiple languages detected in merge request");
	        return new MergeResult(false, "Multiple languages detected in merge request", message.RequestingUserName);
		}

		var mergedManifest = new ResourceContainer()
		{
			dublin_core = new DublinCore()
			{
				conformsto = "rc0.2",
				contributor = contributors.Distinct().ToArray(),
				format = "text/usfm",
				language = new Language()
				{
					direction = languageDirection,
					identifier = languageCodes.First(),
					title = languageName,
				},
				identifier = "reg",
				publisher = "Wycliffe Associates",
				relation = [],
				rights = "CC BY-SA 4.0",
				source = sources.Distinct().ToArray(),
				subject = "Bible",
				title = null, // We don't have any idea what the whole Bible should be called in this language so we'll let the renderer guess
				type = "bundle",
				version = "0.1",
				modified = DateTime.Now.ToString("yyyy-MM-dd")
			},
			projects = projects.ToArray(),
		};
		
		var serializer = new SerializerBuilder().Build();
		output.Add("manifest.yaml", serializer.Serialize(mergedManifest));

		var languageCode = languageCodes.First();
        var repoName = $"merged-{languageCode}";
        _log.LogInformation("Uploading into {User}/{Repo}", _destinationUser, repoName);
        
        // Create a scripture burrito for thie merged repo
        var burrito = CreateBurrito("Bible", "bible", languageCode, languageName, languageDirection, contentForBurrito.OrderBy(i => Utils.GetBookNumber(i.BookCode)).ToList());
        
        output.Add("metadata.json", BurritoSerializer.Serialize(burrito));
        var existingRepo = await _giteaClient.GetRepository(_destinationUser, repoName);
        if (existingRepo != null)
		{
			_log.LogWarning("Repository already exists");
			var newBranchName = $"{message.RequestingUserName}-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}";
			await UploadContentToBranch(_destinationUser, repoName, newBranchName, output);
			return new MergeResult(true, $"{_giteaBaseAddress}/{_destinationUser}/{repoName}/src/branch/{newBranchName}", message.RequestingUserName,
				languageCode, _destinationUser, repoName, existingRepo.Id, mergedPORTRepoIds);
		}
        

        var createdRepoId = await UploadContentToNewRepo(_destinationUser, repoName, output);
        return new MergeResult(true, $"{_giteaBaseAddress}/{_destinationUser}/{repoName}", message.RequestingUserName,
	        languageCode, _destinationUser, repoName, createdRepoId, mergedPORTRepoIds);
    }

    private static async Task MergeUSFMProject(ZipFileSystem projectZip, Dictionary<string, string> output, List<ContentForBurrito> outputForBurrito)
    {
	    var parser = new USFMParser(ignoreUnknownMarkers: true);
	    foreach (var file in projectZip.GetAllFiles(".usfm"))
	    {
		    var content = await projectZip.ReadAllTextAsync(file);
		    var doc = parser.ParseFromString(content);
		    var toc1 = doc.GetChildMarkers<TOC1Marker>().FirstOrDefault();
		    var toc2 = doc.GetChildMarkers<TOC2Marker>().FirstOrDefault();
		    var toc3 = doc.GetChildMarkers<TOC3Marker>().FirstOrDefault();
		    var fileName = Path.GetFileName(file);
		    outputForBurrito.Add(new()
		    {
			    BookCode = toc3.BookAbbreviation.ToUpper(),
			    BookLongName = toc1.LongTableOfContentsText,
			    BookName = toc2.ShortTableOfContentsText,
			    Path = fileName,
			    Size = (uint)content.Length,
                MD5Hash = HashString(content),
		    });
		    output.TryAdd(fileName, content);
	    }
    }

    private static string HashString(string input)
    {
	    return MD5.HashData(System.Text.Encoding.Unicode.GetBytes(input))
		    .Aggregate("", (s, b) => s + b.ToString("x2"));
    }

    private void MergeWriterProject(ZipFileSystem projectZip, string? basePath, MergeRequestRepo repo,
	    Dictionary<string, string> output,
	    USFMRenderer renderer, List<ContentForBurrito> contentForBurrito)
    {
	    var container = new ZipFileSystemBTTWriterLoader(projectZip,basePath);
	    var usfmObject = BTTWriterLoader.CreateUSFMDocumentFromContainer(container, false, new USFMParser(ignoreUnknownMarkers: true));
	    var bookCode = usfmObject.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation;
	    
	    if (bookCode == null)
	    {
		    _log.LogWarning("No book code found for {User}/{Repo}", repo.User, repo.Repo);
		    return;
	    }
	    
	    var fileName = BuildWriterUSFMFileName(bookCode);

	    if (output.ContainsKey(fileName))
	    {
		    return;
	    }
				
	    var usfm = renderer.Render(usfmObject);
	    contentForBurrito.Add(new()
	    {
		    BookCode = bookCode.ToUpper(),
		    BookName = usfmObject.GetChildMarkers<TOC2Marker>().FirstOrDefault()?.ShortTableOfContentsText ?? bookCode,
		    BookLongName = usfmObject.GetChildMarkers<TOC1Marker>().FirstOrDefault()?.LongTableOfContentsText ?? bookCode,
		    MD5Hash = HashString(usfm),
		    Size = (uint)usfm.Length,
		    Path = fileName,
	    });
	    
	    output.Add(fileName, usfm);
    }

    private static string BuildWriterUSFMFileName(string bookCode)
    {
	    var bookIndex = Utils.GetBookNumber(bookCode);
	    return $"{(bookIndex == 0 ? "" : $"{bookIndex}-")}{bookCode.ToUpper()}.usfm";
    }

    private async Task<int> UploadContentToNewRepo(string user, string repoName, Dictionary<string,string> content)
    {
		var createdRepo = await _giteaClient.IsOrganization(user)
			? await _giteaClient.CreateRepositoryInOrganization(user, repoName)
			: await _giteaClient.CreateRepository(user, repoName);
		await _giteaClient.UploadMultipleFiles(user,repoName,content);
		return createdRepo!.Id;
    }

    private async Task UploadContentToBranch(string user, string repoName, string branch, Dictionary<string, string> content)
    {
	    var branchExists = await _giteaClient.BranchExists(user, repoName, branch);
	    if (!branchExists)
	    {
		    await _giteaClient.CreateBranch(user, repoName, branch);
	    }
	    await _giteaClient.UploadMultipleFiles(user, repoName, content, branch);
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
    private static SerializationRoot CreateBurrito(string projectName, string projectAbbreviation, string languageCode, string languageName, string languageTextDirection, List<ContentForBurrito> content)
    {
        return new SerializationRoot()
        {
            Meta = new Meta()
            {
                Version = "1.0.0",
                Category = "source",
                Generator = new ScriptureBurrito.Models.Generator() 
                {
                    SoftwareName = "Repo consolidator",
                    SoftwareVersion = "1.0.0",
                    UserName = "rbnswartz"
                },
                DefaultLocale = "en",
                DateCreated = DateTime.Now,
                Normalization = "NFC"
            },
            IdAuthorities = new()
            {
                ["wycliffeassociates"] = new Authority()
                {
                    Id = "https://www.wycliffeassociates.org",
                    Name = new Dictionary<string, string>()
                    {
                        ["en"] = "Wycliffe Associates"
                    }
                }
            },
            Identification = new Identification()
            {
                Primary = new()
                {
                    ["wycliffeassociatse"] = new()
                    {
                        [Guid.NewGuid().ToString()] = new Revision()
                        {
                            RevisionId = "1.0.0",
                            TimeStamp = DateTime.Now
                        }
                    }
                },
                Name = new()
                {
                    ["en"] = projectName,
                },
                Abbreviation = new()
                {
                    ["en"] = projectAbbreviation,
                }
            },
            Confidential = false,
            Languages = [
                new ScriptureBurrito.Models.Language()
                {
                    Tag = languageCode,
                    Name = new Dictionary<string, string>()
                    {
                        ["en"] = languageName,
                    },
                    ScriptDirection = languageTextDirection
                }
            ],
            Type = new()
            {
                FlavorType = new()
                {
                    Name = "scripture",
                    Flavor = new Flavor()
                    {
                        Name = "textTranslation",
                        ProjectType = "standard",
                        TranslationType = "firstTranslation",
                        Audience = "common",
                        UsfmVersion = "3.0"
                    },
                    CurrentScope = content.ToDictionary(i => i.BookCode, i => new List<string>())
                }
            },
            Copyright = new()
            {
                Licenses = new List<ScriptureBurrito.Models.License>()
                {
                    new ScriptureBurrito.Models.License()
                    {
                        Ingredient = "LICENSE.md",
                    }
                }
            }, //Let's see if this works without it
            LocalizedNames = content.ToDictionary(i => i.BookCode, i => new LocalizedName()
            {
                // TODO: Bring in other things instead of just the book code
                Short = new ()
                {
                    ["en"] = i.BookCode
                },
                Abbreviation = new ()
                {
                    ["en"] = i.BookCode
                },
                Long = new ()
                {
                    ["en"] = i.BookCode
                }
            }),
            Ingredients = content.ToDictionary(i => i.Path, i => new Ingredient()
            {
                Checksum = new Checksum()
                {
                    MD5 = i.MD5Hash
                },
                MimeType = "text/x-usfm",
                Size = i.Size,
                Scope = new ()
                {
                    [i.BookCode] = new()
                }
            })
        };
    }
}
public class ContentForBurrito
{
    public string Path { get; set; }
    public string BookCode { get; set; }
    public string MD5Hash { get; set; }
    public uint Size { get; set; }
    public string BookName { get; set; }
    public string BookLongName { get; set; }
}
