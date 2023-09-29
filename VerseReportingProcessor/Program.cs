// See https://aka.ms/new-console-template for more information

using System.Data;
using System.Data.SqlClient;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using PipelineCommon.Models;
using VerseReportingProcessor.Models;
using PipelineCommon.Models.BusMessages;

public static class Program
{
    public static async Task Main()
    {
	    var upsertTopic = "VerseCountingResult";
	    var deleteTopic = "";
	    var subscriptionName = "InternalProcessor";
	    var client = new ServiceBusClient(GetServiceBusConnectionString(),
		    new ServiceBusClientOptions() { TransportType = ServiceBusTransportType.AmqpWebSockets });
	    var upsertProcessor = client.CreateProcessor(upsertTopic, subscriptionName);
	    upsertProcessor.ProcessMessageAsync += async args =>
	    {
		    var body = args.Message.Body.ToString();
			var input = JsonSerializer.Deserialize<VerseCountingResult>(body);
			var result = Calculate(input, await GetCountDefinitionsAsync(input.LanguageCode));
			await SendToDatabase(result);
		    await args.CompleteMessageAsync(args.Message);
	    };
	    
	    upsertProcessor.ProcessErrorAsync += args =>
	    {
		    Console.WriteLine(args.Exception.ToString());
		    return Task.CompletedTask;
	    };
	    await upsertProcessor.StartProcessingAsync();
	    Console.ReadLine();
	    await upsertProcessor.StopProcessingAsync();

    }

    private static string GetSQLConnectionString()
    {
        return "";
    }
    private static string GetServiceBusConnectionString()
	{
		return "";
	}

    private static async Task SendToDatabase(DBModel input)
    {
	    var connection = new SqlConnection(GetSQLConnectionString());
	    var command = new SqlCommand("exec [dbo].[VerseCountingResult] @result", connection);
	    var parameter = new SqlParameter("result", SqlDbType.NVarChar);
	    parameter.Value = JsonSerializer.Serialize(input);
	    command.Parameters.Add(parameter);
	    await command.ExecuteNonQueryAsync();
    }

    private static DBModel Calculate(VerseCountingResult input, CountDefinitions countDefinitions)
    {
	    var output = new DBModel()
	    {
		    LanguageCode = input.LanguageCode,
		    User = input.User,
		    Repo = input.Repo,
		    RepoId = input.RepoId
	    };
	    foreach (var book in input.Books)
	    {
		    var outputBook = new DBBook()
		    {
			    Slug = book.BookId.ToLower(),
		    };
		    output.Books.Add(outputBook);
		    var currentBookDefinitions = countDefinitions.Books[book.BookId.ToLower()];
		    outputBook.ExpectedChapters = currentBookDefinitions.ExpectedChapters;
		    outputBook.ActualChapters = book.Chapters.Count;
		    foreach (var chapter in book.Chapters)
		    {
			    var outputChapter = new DBChapter();
			    outputChapter.Number = chapter.ChapterNumber;
			    outputChapter.ActualVerses = chapter.VerseCount;
			    outputChapter.ExpectedVerses = currentBookDefinitions.ExpectedChapterCounts[chapter.ChapterNumber];
			    outputBook.Chapters.Add(outputChapter);
		    }
	    }

	    return output;
    }
    
    private static async Task<CountDefinitions> GetCountDefinitionsAsync(string languageCode)
    {
	    var client = new BlobContainerClient(GetBlobConnectionString(), "versecounts");
	    var source = client.GetBlobClient($"{languageCode}.json");
	    if (!await source.ExistsAsync())
	    {
		    source = client.GetBlobClient("default.json");
	    }

	    var stream = await source.OpenReadAsync();
	    return await JsonSerializer.DeserializeAsync<CountDefinitions>(stream) ?? new CountDefinitions();
    }
    private static string GetBlobConnectionString()
	{
		return "UseDevelopmentStorage=true;";
	}
}