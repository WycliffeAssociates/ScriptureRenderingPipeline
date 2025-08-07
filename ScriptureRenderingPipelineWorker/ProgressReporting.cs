using System.Net;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using BTTWriterLib;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using PipelineCommon.Helpers;
using PipelineCommon.Models;
using PipelineCommon.Models.BusMessages;
using USFMToolsSharp.Models.Markers;

namespace ScriptureRenderingPipelineWorker;

public class ProgressReporting
{
    private ILogger<ProgressReporting> log;
    private readonly ServiceBusClient client;
    public ProgressReporting(ILogger<ProgressReporting> logger, IAzureClientFactory<ServiceBusClient> serviceBusClientFactory)
    {
        log = logger;
        client = serviceBusClientFactory.CreateClient("ServiceBusClient");
    }
    [Function("ProgressReporting")]
    [ServiceBusOutput("VerseCountingResult", Connection = "ServiceBusConnectionString")]
    public async Task RunAsync([ServiceBusTrigger("WACSEvent", "VerseCounting", IsSessionsEnabled = false, Connection = "ServiceBusConnectionString")] string messageText)
    {
        var message = JsonSerializer.Deserialize(messageText, WorkerJsonContext.Default.WACSMessage);
        var countResult = await CountVersesAsync(log, message);
        var output =
            new ServiceBusMessage(JsonSerializer.Serialize(countResult, WorkerJsonContext.Default.VerseCountingResult))
                {
                    ApplicationProperties =
                    {
                        ["Success"] = countResult.Success
                    }
                };
        await using var sender = client.CreateSender("VerseCountingResult");
        await sender.SendMessageAsync(output);
    }


    private static async Task<VerseCountingResult> CountVersesAsync(ILogger log, WACSMessage message)
    {
        log.LogInformation("Counting Verses for {Username}/{Repo}", message.User, message.Repo);
        var fileResult = await Utils.httpClient.GetAsync(Utils.GenerateDownloadLink(message.RepoHtmlUrl, message.User, message.Repo, message.DefaultBranch));
        
	    log.LogDebug("Got status code: {StatusCode}", fileResult.StatusCode);
        
        if (fileResult.StatusCode == HttpStatusCode.NotFound)
        {
	        log.LogWarning("Repo not found or is empty");
            return new VerseCountingResult(message)
            {
                Success = false,
                Message = "Repo not found or is empty"
            };
        }
        if (!fileResult.IsSuccessStatusCode)
        {
            log.LogError("Failed to download repo: {StatusCode}", fileResult.StatusCode);
		    throw new HttpRequestException("Got an unexpected response from Gitea expected 200 or 404 but got " + fileResult.StatusCode)
		    {
			    Data = { ["RepositoryUrl"] = message.RepoHtmlUrl, ["StatusCode"] = fileResult.StatusCode }
		    };
        }
        
        var zipStream = await fileResult.Content.ReadAsStreamAsync();
        var fileSystem = new ZipFileSystem(zipStream);
        var basePath = fileSystem.GetFolders().FirstOrDefault();
        RepoIdentificationResult details;
        try
        {
            details = await Utils.GetRepoInformation(log, fileSystem, basePath, message.Repo);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error getting repo information");
            return new VerseCountingResult(message)
            {
                Success = false,
                Message = $"Error getting repo information {ex.Message}"
            };
        }

        if (details.repoType != RepoType.Bible)
        {
            return new VerseCountingResult(message)
            {
                Success = false,
                Message = "Not scripture, skipping"
            };
        }

        var files = new List<USFMDocument>();
        try
        {
            if (details.isBTTWriterProject)
            {
                var loader = new ZipFileSystemBTTWriterLoader(fileSystem, basePath);
                var document = BTTWriterLoader.CreateUSFMDocumentFromContainer(loader, false);
                files.Add(document);
            }
            else
            {
                files = await Utils.LoadUsfmFromDirectoryAsync(fileSystem);
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error loading USFM files");
            return new VerseCountingResult(message)
            {
                Success = false,
                Message = $"Error loading USFM files {ex.Message}"
            };
        }

        var output = new VerseCountingResult(message)
        {
            Success = true,
            LanguageCode = details.languageCode
        };
        foreach (var file in files)
        {
            var bookId = file.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation;
            try
            {
                var chapters = file.GetChildMarkers<CMarker>();
                var outputBook = new VerseCountingBook()
                {
                    BookId = bookId
                };
                output.Books.Add(outputBook);

                foreach (var chapter in chapters)
                {
                    var verseCount = Utils.CountUniqueVerses(chapter);

                    outputBook.Chapters.Add(new VerseCountingChapter
                    {
                        ChapterNumber = chapter.Number,
                        VerseCount = verseCount,
                    });
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error counting verses in {Book}", bookId);
                output.Success = false;
                output.Message = $"Error counting verses in {bookId}: {ex.Message}";
            }
        }

        return output;
    }

}