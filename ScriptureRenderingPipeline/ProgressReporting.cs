using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using BTTWriterLib;
using Microsoft.Azure.Amqp.Serialization;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using PipelineCommon.Helpers;
using USFMToolsSharp;
using USFMToolsSharp.Models.Markers;
using PipelineCommon.Models.BusMessages;

namespace ScriptureRenderingPipeline;

public static class ProgressReporting
{
    [FunctionName("ProgressReporting")]
    [ServiceBusAccount("ServiceBusConnectionString")]
    [return:ServiceBus("VerseCountingResult")]
    public static async Task<ServiceBusMessage> RunAsync([ServiceBusTrigger("WACSEvent", "VerseCounting")] string messageText,
        ILogger log)
    {
	    var message = JsonSerializer.Deserialize<WACSMessage>(messageText);
	    var countResult = await CountVersesAsync(log, message);
	    var output = new ServiceBusMessage(JsonSerializer.Serialize(countResult));
	    output.ApplicationProperties["Success"] = countResult.Success;
	    return output;
    }

    private static async Task<VerseCountingResult> CountVersesAsync(ILogger log, WACSMessage message)
    {
	    var httpClient = new HttpClient();
	    var fileResult = await httpClient.GetAsync($"{message.RepoHtmlUrl}/archive/master.zip");
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
		    files = await LoadDirectoryAsync(fileSystem);
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
			    var verseSelection = new HashSet<int>();
			    var verses = chapter.GetChildMarkers<VMarker>();
			    foreach (var verse in verses)
			    {
				    if (verse.StartingVerse == verse.EndingVerse)
				    {
					    verseSelection.Add(verse.StartingVerse);
					    continue;
				    }

				    for (var i = verse.StartingVerse; i < verse.EndingVerse; i++)
				    {
					    verseSelection.Add(i);
				    }
			    }

			    outputBook.Chapters.Add(new()
			    {
				    ChapterNumber = chapter.Number,
				    VerseCount = verseSelection.Count,
			    });
		    }
	    }

	    return output;
    }

    static async Task<List<USFMDocument>> LoadDirectoryAsync(ZipFileSystem directory)
	{
		var parser = new USFMParser(new List<string> { "s5" }, true);
		var output = new List<USFMDocument>();
		foreach (var f in directory.GetAllFiles(".usfm"))
		{
			var tmp = parser.ParseFromString(await directory.ReadAllTextAsync(f));
			// If we don't have an abbreviation then try to figure it out from the file name
			var tableOfContentsMarkers = tmp.GetChildMarkers<TOC3Marker>();
			if (tableOfContentsMarkers.Count == 0)
			{
				var bookAbbreviation = GetBookAbberviationFromFileName(f);
				if (bookAbbreviation != null)
				{
					tmp.Insert(new TOC3Marker() { BookAbbreviation = bookAbbreviation });
				}
			}
			else if (Utils.GetBookNumber(tableOfContentsMarkers[0].BookAbbreviation) == 0)
			{
				var bookAbbreviation = GetBookAbberviationFromFileName(f);
				if (bookAbbreviation != null)
				{
					tableOfContentsMarkers[0].BookAbbreviation = bookAbbreviation;
				}
			}
			output.Add(tmp);
		}
		return output;
	}
		private static string GetBookAbberviationFromFileName(string f)
		{
			string bookAbbreviation = null;
			var fileNameSplit = Path.GetFileNameWithoutExtension(f).Split('-');
			if (fileNameSplit.Length == 2)
			{
				if (Utils.BibleBookOrder.Contains(fileNameSplit[1].ToUpper()))
				{
					bookAbbreviation = fileNameSplit[1].ToUpper();
				}
			}
			else if (fileNameSplit.Length == 1)
			{
				if (Utils.BibleBookOrder.Contains(fileNameSplit[0].ToUpper()))
				{
					bookAbbreviation = fileNameSplit[0].ToUpper();
				}
			}

			return bookAbbreviation;
		}
}