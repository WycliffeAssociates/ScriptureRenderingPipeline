using System.Data;
using System.Data.SqlClient;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Hosting;
using PipelineCommon.Models;
using PipelineCommon.Models.BusMessages;
using VerseReportingProcessor.Models;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VerseReportingProcessor;

public class DbProcessor: IHostedService
{
	private ServiceBusProcessor upsertProcessor;
	private ServiceBusProcessor deleteProcessor;
	private readonly string upsertTopic = "VerseCountingResult";
	private readonly string subscriptionName = "InternalProcessor";
	private readonly string deleteTopic = "WACSEvent";
	private readonly IConfiguration _config;
	private readonly ILogger _log;

	public DbProcessor(IConfiguration config, ILogger<DbProcessor> log)
	{
		_config = config;
		_log = log;
	}
	
    public async Task StartAsync(CancellationToken cancellationToken)
    {
	    var client = new ServiceBusClient(GetServiceBusConnectionString(),
		    new ServiceBusClientOptions() { TransportType = ServiceBusTransportType.AmqpWebSockets });
	    upsertProcessor = client.CreateProcessor(upsertTopic, subscriptionName);
	    deleteProcessor = client.CreateProcessor(deleteTopic, subscriptionName);
	    
	    upsertProcessor.ProcessMessageAsync += async args =>
	    {
		    var body = args.Message.Body.ToString();
			var input = JsonSerializer.Deserialize<VerseCountingResult>(body);
			if (input == null)
			{
				throw new Exception("Invalid message received");
			}
			_log.LogInformation("Processing {RepoId} {User}/{Repo}", input.RepoId.ToString(), input.Repo, input.User);
			var result = Calculate(input, await GetCountDefinitionsAsync(input.LanguageCode));
			await SendUpsertToDatabaseAsync(result);
		    await args.CompleteMessageAsync(args.Message, cancellationToken);
	    };
	    
	    upsertProcessor.ProcessErrorAsync += args =>
	    {
		    _log.LogError("Error in processing upsert {Error}", args.Exception.ToString());
		    return Task.CompletedTask;
	    };

		deleteProcessor.ProcessMessageAsync += async args =>
		{
			var body = args.Message.Body.ToString();
			var input = JsonSerializer.Deserialize<VerseCountingResult>(body);
			if (input == null)
			{
				throw new Exception("Invalid message received");
			}

			_log.LogInformation("Processing delete for {RepoId} {User}/{Repo}", input.RepoId.ToString(), input.Repo,
				input.User);
			await SendDeleteToDatabaseAsync(input.RepoId);
		};
		
	    deleteProcessor.ProcessErrorAsync += args =>
	    {
		    _log.LogError("Error in processing delete {Error}", args.Exception.ToString());
		    return Task.CompletedTask;
	    };
	    
	    await upsertProcessor.StartProcessingAsync(cancellationToken);
		_log.LogInformation("Started upsert Listener");
	    await deleteProcessor.StartProcessingAsync(cancellationToken);
		_log.LogInformation("Started delete Listener");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
	    await upsertProcessor.StopProcessingAsync(cancellationToken);
		_log.LogInformation("Stopped upsert Listener");
		await deleteProcessor.StopProcessingAsync(cancellationToken);
		_log.LogInformation("Stopped delete Listener");
    }
    
    private string? GetSqlConnectionString()
    {
        return _config.GetConnectionString("Database");
    }
    private string? GetServiceBusConnectionString()
    {
	    return _config.GetConnectionString("ServiceBus");
	}

    private async Task SendUpsertToDatabaseAsync(DBModel input)
    {
	    var connection = new SqlConnection(GetSqlConnectionString());
	    var command = new SqlCommand("exec [dbo].[VerseCountingResult] @result", connection);
	    var parameter = new SqlParameter("result", SqlDbType.NVarChar)
	    {
		    Value = JsonSerializer.Serialize(input)
	    };
	    await connection.OpenAsync();
	    command.Parameters.Add(parameter);
	    await command.ExecuteNonQueryAsync();
	    await connection.CloseAsync();
    }

    private async Task SendDeleteToDatabaseAsync(int repoId)
    {
	    var connection = new SqlConnection(GetSqlConnectionString());
	    var command = new SqlCommand("exec [dbo].[RepoDeleted] @id", connection);
	    var parameter = new SqlParameter("id", SqlDbType.Int)
	    {
		    Value = repoId
	    };
	    await connection.OpenAsync();
	    command.Parameters.Add(parameter);
	    await command.ExecuteNonQueryAsync();
	    await connection.CloseAsync();
    }

    private DBModel Calculate(VerseCountingResult input, CountDefinitions countDefinitions)
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
    
    private async Task<CountDefinitions> GetCountDefinitionsAsync(string languageCode)
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
    private string? GetBlobConnectionString()
	{
		return _config.GetConnectionString("BlobStorage");
	}
}