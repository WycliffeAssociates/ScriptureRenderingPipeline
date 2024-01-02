using System.Net;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using BTTWriterLib;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using PipelineCommon.Helpers;
using PipelineCommon.Models.BusMessages;
using USFMToolsSharp.Models.Markers;

namespace ScriptureRenderingPipelineWorker;

public class ProgressReporting
{
    private ILogger<ProgressReporting> log;
    private ServiceBusClient client;
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
        var sender = client.CreateSender("VerseCountingResult");
        var output =
            new ServiceBusMessage(JsonSerializer.Serialize(countResult, WorkerJsonContext.Default.VerseCountingResult))
                {
                    ApplicationProperties =
                    {
                        ["Success"] = countResult.Success
                    }
                };
        await sender.SendMessageAsync(output);
        
    }


    private static async Task<VerseCountingResult> CountVersesAsync(ILogger log, WACSMessage message)
    {
        log.LogInformation("Counting Verses for {Username}/{Repo}", message.User, message.Repo);
        var fileResult = await Utils.httpClient.GetAsync(Utils.GenerateDownloadLink(message.RepoHtmlUrl, message.User, message.Repo));
        
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
        
        var zipStream = await fileResult.Content.ReadAsStreamAsync();
        var fileSystem = new ZipFileSystem(zipStream);
        var basePath = fileSystem.GetFolders().FirstOrDefault();
        var details = await Utils.GetRepoInformation(log, fileSystem, basePath, message.Repo);

        if (details.repoType != RepoType.Bible)
        {
            return new VerseCountingResult(message)
            {
                Success = false,
                Message = "Not scripture, skipping"
            };
        }

        var files = new List<USFMDocument>();
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

        var output = new VerseCountingResult(message)
        {
            Success = true,
            LanguageCode = details.languageCode
        };
        foreach (var file in files)
        {
            var bookId = file.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation;
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

        return output;
    }

}