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
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace VerseReportingProcessor;

public class VerseCounterService: IHostedService
{
	private ServiceBusProcessor? upsertProcessor;
	private ServiceBusProcessor? deleteProcessor;
	private readonly string upsertTopic = "VerseCountingResult";
	private readonly string subscriptionName = "InternalProcessor";
	private readonly string deleteTopic = "WACSEvent";
	private readonly IConfiguration _config;
	private readonly ILogger _log;

	public VerseCounterService(IConfiguration config, ILogger<VerseCounterService> log)
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
			_log.LogInformation("Processing {RepoId} {User}/{Repo}", input.RepoId.ToString(), input.User, input.Repo);
			var result = Calculate(input, await GetCountDefinitionsAsync(input.LanguageCode));
			await SendUpsertToDatabaseAsync(result);


			var service = new ServiceClient(GetPortConnectionString());
			await UpsertIntoPORT(service, result);
			
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
	    if (upsertProcessor != null)
	    {
			await upsertProcessor.StopProcessingAsync(cancellationToken);
			_log.LogInformation("Stopped upsert Listener");
	    }
	    else
	    {
		    _log.LogError("Upsert processor was not started so we're going to skip stopping");
	    }

	    if (deleteProcessor != null)
	    {
			await deleteProcessor.StopProcessingAsync(cancellationToken);
			_log.LogInformation("Stopped delete Listener");
	    }
	    else
	    {
		    _log.LogError("Delete processor was not started so we're going to skip stopping");
	    }
    }
    
    private string? GetSqlConnectionString()
    {
        return _config.GetConnectionString("Database");
    }
    private string? GetServiceBusConnectionString()
    {
	    return _config.GetConnectionString("ServiceBus");
	}

    private async Task SendUpsertToDatabaseAsync(ComputedResult input)
    {
	    var connection = new SqlConnection(GetSqlConnectionString());
	    var command = new SqlCommand("exec [Gogs2].[p_Merge_Repo_Book_Chapter_JSON] @RepoBookChapterJson", connection);
	    var parameter = new SqlParameter("RepoBookChapterJson", SqlDbType.NVarChar)
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
	    var command = new SqlCommand("exec [Gogs2].[p_Delete_Repo_Book_Chapter] @RepoId", connection);
	    var parameter = new SqlParameter("RepoId", SqlDbType.Int)
	    {
		    Value = repoId
	    };
	    await connection.OpenAsync();
	    command.Parameters.Add(parameter);
	    await command.ExecuteNonQueryAsync();
	    await connection.CloseAsync();
    }
    
    private string? GetPortConnectionString()
	{
	    return _config.GetConnectionString("Dataverse");
	}

    private async Task UpsertIntoPORT(ServiceClient service, ComputedResult input)
    {
	    var query = new QueryExpression("wa_translationrepo")
	    {
		    ColumnSet = new ColumnSet("wa_expectedverses", "wa_actualverses")
	    };
	    var (expected, actual) = CalculateTotalVerses(input);
	    query.Criteria.AddCondition("wa_wacsid", ConditionOperator.Equal, input.RepoId);
	    var result = await service.RetrieveMultipleAsync(query);
	    if (result.Entities.Count > 0)
	    {
		    var repo = result.Entities[0];
		    if (repo.Contains("wa_expectedverses"))
		    {
			    if ((int)repo["wa_expectedverses"] != expected)
			    {
				    repo["wa_exptectedverses"] = expected;
			    }
			    else
			    {
				    repo.Attributes.Remove("wa_expectedverses");
			    }
			    
		    }
		    else
		    {
			    repo["wa_expectedverses"] = expected;
		    }

		    if (repo.Contains("wa_actualverses"))
		    {
			    if ((int)repo["wa_actualverses"] != actual)
			    {
				    repo["wa_actualverses"] = actual;
			    }
			    else
			    {
				    repo.Attributes.Remove("wa_actualverses");
			    }
			    
		    }
		    else
		    {
			    repo["wa_actualverses"] = actual;
		    }

		    await service.UpdateAsync(repo);
	    }
	    else
	    {
		    await service.CreateAsync(new Entity("wa_translationrepo", Guid.NewGuid())
		    {
			    ["wa_actualverses"] = actual,
			    ["wa_expectedverses"] = expected,
			    ["wa_wacsid"] = input.RepoId,
			    ["wa_user_id"] = input.User,
			    ["wa_repo_id"] = input.Repo,
			    ["wa_name"] = $"{input.User}/{input.Repo}"
		    });
	    }
    }

    private (int expected, int actual) CalculateTotalVerses(ComputedResult input)
    {
	    var actual = 0;
	    var expected = 0;

	    foreach (var book in input.Books)
	    {
		    foreach (var chapter in book.Chapters)
		    {
			    actual += chapter.ActualVerses;
			    expected += chapter.ExpectedVerses;
		    }
	    }

	    return (expected, actual);
    }

    private ComputedResult Calculate(VerseCountingResult input, CountDefinitions countDefinitions)
    {
	    var output = new ComputedResult()
	    {
		    LanguageCode = input.LanguageCode,
		    User = input.User,
		    Repo = input.Repo,
		    RepoId = input.RepoId
	    };
	    foreach (var (book, bookCountDefinition) in countDefinitions.Books)
	    {
		    var outputBook = new ComputedResultBook()
		    {
			    Slug = book
		    };
		    output.Books.Add(outputBook);
		    outputBook.ExpectedChapters = bookCountDefinition.ExpectedChapters;
		    var currentBook = input.Books.FirstOrDefault(b => b.BookId == book);
		    if (currentBook == null)
		    {
			    outputBook.ActualChapters = 0;
		    }
		    foreach (var (chapter, verseCounts) in bookCountDefinition.ExpectedChapterCounts)
		    {
			    var outputChapter = new ComputedResultChapter
			    {
				    Number = chapter,
				    ActualVerses = currentBook?.Chapters.FirstOrDefault(c => c.ChapterNumber == chapter)?.VerseCount ?? 0,
				    ExpectedVerses = verseCounts
			    };
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