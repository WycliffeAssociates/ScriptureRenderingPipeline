using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Hosting;
using PipelineCommon.Models;
using PipelineCommon.Models.BusMessages;
using VerseReportingProcessor.Models;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using PipelineCommon.Helpers;

namespace VerseReportingProcessor;

public class VerseCounterService: IHostedService
{
	private ServiceBusProcessor? _upsertProcessor;
	private ServiceBusProcessor? _deleteProcessor;
	private const string UpsertTopic = "VerseCountingResult";
	private const string SubscriptionName = "InternalProcessor";
	private const string DeleteTopic = "WACSEvent";
	private readonly IConfiguration _config;
	private readonly ILogger _log;
	private readonly IMemoryCache _cache;
	private readonly ActivitySource _activitySource;
	private readonly VerseProcessorMetrics _metrics;
	private readonly OrganizationServiceFactory _organizationServiceFactory;
	private readonly GiteaClient _giteaClient;
	private readonly SemaphoreSlim _databaseSemaphore = new(1, 1);
	private readonly bool _enableDatabasePush;
	private readonly bool _enablePortPush;

	public VerseCounterService(IConfiguration config, ILogger<VerseCounterService> log, IMemoryCache cache, VerseProcessorMetrics metrics, OrganizationServiceFactory organizationServiceFactory, GiteaClient  giteaClient)
	{
		_config = config;
		_log = log;
		_cache = cache;
		_activitySource = new ActivitySource(nameof(VerseCounterService));
		_metrics = metrics;
		_organizationServiceFactory = organizationServiceFactory;
		_enableDatabasePush = config.GetValue("EnableDatabasePush", true);
		_enablePortPush = config.GetValue("EnablePortPush", true);
		_giteaClient = giteaClient;
	}
	
    public async Task StartAsync(CancellationToken cancellationToken)
    {
	    var client = new ServiceBusClient(GetServiceBusConnectionString(),
		    new ServiceBusClientOptions() { TransportType = ServiceBusTransportType.AmqpWebSockets });
	    _upsertProcessor = client.CreateProcessor(UpsertTopic, SubscriptionName, new ServiceBusProcessorOptions()
	    {
		    MaxConcurrentCalls = _config.GetValue("MaxServiceBusConnections", 1),
	    });
	    _deleteProcessor = client.CreateProcessor(DeleteTopic, SubscriptionName, new ServiceBusProcessorOptions()
	    {
		    MaxConcurrentCalls = _config.GetValue("MaxServiceBusConnections", 1),
	    });
	    
	    _upsertProcessor.ProcessMessageAsync += async args =>
	    {
		    var parentActivityId = args.Message.ApplicationProperties.TryGetValue("Diagnostic-Id", out var diagnosticId) ? diagnosticId as string : null ;
		    using var activity = _activitySource.StartActivity("UpsertProcessing", ActivityKind.Consumer, parentActivityId);
		    var body = args.Message.Body.ToString();
			var input = JsonSerializer.Deserialize<VerseCountingResult>(body);
			if (input?.LanguageCode == null || input.RepoId == 0 || input.RepoId == null || input.Repo == null)
			{
				throw new Exception("Invalid message received");
			}
			_log.LogInformation("Processing {RepoId} {User}/{Repo}", input.RepoId.ToString(), input.User, input.Repo);
			var result = Calculate(input, await GetCountDefinitionsAsync(input.LanguageCode));
			var tasks = new List<Task>();
			if (_enableDatabasePush)
				tasks.Add(SendUpsertToDatabaseAsync(result));
			if (_enablePortPush)
			{
				var service = await _organizationServiceFactory.GetServiceClientAsync();
				tasks.Add(UpsertIntoPORT(service, result, input.RepoHtmlUrl, input.Analysis));
			}
			await Task.WhenAll(tasks);
			
			_metrics.ReposProcessed(1);
		    await args.CompleteMessageAsync(args.Message, cancellationToken);
	    };
	    
	    _upsertProcessor.ProcessErrorAsync += args =>
	    {
		    _log.LogError("Error in processing upsert {Error}", args.Exception.ToString());
		    return Task.CompletedTask;
	    };

		_deleteProcessor.ProcessMessageAsync += async args =>
		{
		    var parentActivityId = args.Message.ApplicationProperties.TryGetValue("Diagnostic-Id", out var diagnosticId) ? diagnosticId as string : null ;
		    using var activity = _activitySource.StartActivity("DeleteProcessing", ActivityKind.Consumer, parentActivityId);
		    var body = args.Message.Body.ToString();
		    var input = JsonSerializer.Deserialize<VerseCountingResult>(body);
			if (input == null)
			{
				throw new Exception("Invalid message received");
			}
			
			if (input.RepoId == 0 || input.RepoId == null)
			{
				_log.LogError("Received delete message with invalid RepoId: {RepoId}", input?.RepoId);
				return;
			}

			_log.LogInformation("Processing delete for {RepoId} {User}/{Repo}", input.RepoId.ToString(), input.User,
				input.Repo);
			var repoStillExists = await _giteaClient.GetRepository(input.RepoId) != null;

			if (repoStillExists)
			{
				_log.LogError("Received delete message for repo {RepoId} but it still exists according to Gitea", input.RepoId);
				return;
			}
			
			var tasks = new List<Task>();
			if (_enableDatabasePush)
			{
				tasks.Add(SendDeleteToDatabaseAsync(input.RepoId));
			}
			if (_enablePortPush)
			{
				var service = await _organizationServiceFactory.GetServiceClientAsync();
				tasks.Add(DeleteFromPORT(service, input.RepoId));
			}
			await Task.WhenAll(tasks);
		};
		
	    _deleteProcessor.ProcessErrorAsync += args =>
	    {
		    _log.LogError("Error in processing delete {Error}", args.Exception.ToString());
		    return Task.CompletedTask;
	    };
	    
	    await _upsertProcessor.StartProcessingAsync(cancellationToken);
		_log.LogInformation("Started upsert Listener");
	    await _deleteProcessor.StartProcessingAsync(cancellationToken);
		_log.LogInformation("Started delete Listener");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
	    if (_upsertProcessor != null)
	    {
			await _upsertProcessor.StopProcessingAsync(cancellationToken);
			_log.LogInformation("Stopped upsert Listener");
	    }
	    else
	    {
		    _log.LogError("Upsert processor was not started so we're going to skip stopping");
	    }

	    if (_deleteProcessor != null)
	    {
			await _deleteProcessor.StopProcessingAsync(cancellationToken);
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
	    await _databaseSemaphore.WaitAsync();
	    using var activity = _activitySource.StartActivity();
	    var connection = new SqlConnection(GetSqlConnectionString());
	    try
	    {

		    var command = new SqlCommand("exec [Gogs2].[p_Merge_Repo_Book_Chapter_JSON] @RepoBookChapterJson",
			    connection);
		    var parameter = new SqlParameter("RepoBookChapterJson", SqlDbType.NVarChar)
		    {
			    Value = JsonSerializer.Serialize(input)
		    };
		    await connection.OpenAsync();
		    command.Parameters.Add(parameter);
		    await command.ExecuteNonQueryAsync();
	    }
	    catch
	    {
		    activity?.SetStatus(ActivityStatusCode.Error);
		    throw;
	    }
	    finally
	    {
		    _databaseSemaphore.Release();
		    await connection.CloseAsync();
	    }
	    
    }

    private async Task SendDeleteToDatabaseAsync(int repoId)
    {
	    await _databaseSemaphore.WaitAsync();
	    using var activity = _activitySource.StartActivity();
	    var connection = new SqlConnection(GetSqlConnectionString());
	    try
	    {
		    var command = new SqlCommand("exec [Gogs2].[p_Delete_Repo_Book_Chapter] @RepoId", connection);
		    var parameter = new SqlParameter("RepoId", SqlDbType.Int)
		    {
			    Value = repoId
		    };
		    await connection.OpenAsync();
		    command.Parameters.Add(parameter);
		    await command.ExecuteNonQueryAsync();
	    }
	    catch
	    {
		    activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
	    finally
	    {
		    _databaseSemaphore.Release();
		    await connection.CloseAsync();
	    }
    }
    
    private string? GetPortConnectionString()
	{
	    return _config.GetConnectionString("Dataverse");
	}

    private static readonly string[] AnalysisColumns =
    {
	    "wa_translators", "wa_chunks_finished", "wa_chunks_uploaded", "wa_chunks_expected", "wa_lastcommitdate",
    };

    private async Task UpsertIntoPORT(ServiceClient service, ComputedResult input, string? repoUrl, RepoAnalysisFields? analysis)
    {
	    using var activity = _activitySource.StartActivity();
	    var query = new QueryExpression("wa_translationrepo")
	    {
		    ColumnSet = new ColumnSet(new[] { "wa_expectedverses", "wa_actualverses" }.Concat(AnalysisColumns).ToArray())
	    };
	    var (expected, actual) = CalculateTotalVerses(input);
	    query.Criteria.AddCondition("wa_wacsid", ConditionOperator.Equal, input.RepoId);
	    var result = await service.RetrieveMultipleAsync(query);
	    var matchedByWacsId = result.Entities.Count > 0;
	    if (!matchedByWacsId)
	    {
		    // GogsImporter creates records without wa_wacsid, so fall back to matching on user and repo
		    query.Criteria = new FilterExpression();
		    query.Criteria.AddCondition("wa_source_system", ConditionOperator.Equal, "WACS");
		    query.Criteria.AddCondition("wa_user_id", ConditionOperator.Equal, input.User);
		    query.Criteria.AddCondition("wa_repo_id", ConditionOperator.Equal, input.Repo);
		    result = await service.RetrieveMultipleAsync(query);
	    }

	    var analysisValues = await GetAnalysisValuesAsync(input, analysis);
	    if (result.Entities.Count > 0)
	    {
		    var repo = result.Entities[0];
		    if (repo.Contains("wa_expectedverses"))
		    {
			    if ((int)repo["wa_expectedverses"] != expected)
			    {
				    repo["wa_expectedverses"] = expected;
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

		    if (!matchedByWacsId)
		    {
			    repo["wa_wacsid"] = input.RepoId;
		    }

		    // Only send analysis fields that changed
		    var changes = analysisValues
			    .Where(v => !(repo.Contains(v.Key) && Equals(repo[v.Key], v.Value)))
			    .ToList();
		    foreach (var column in AnalysisColumns)
		    {
			    repo.Attributes.Remove(column);
		    }
		    foreach (var (attribute, value) in changes)
		    {
			    repo[attribute] = value;
		    }

		    await service.UpdateAsync(repo);
	    }
	    else
	    {
		    var language = await PORTUtils.GetLanguageFromCode(service, input.LanguageCode, _log);
		    var repo = new Entity("wa_translationrepo", Guid.NewGuid())
		    {
			    ["wa_actualverses"] = actual,
			    ["wa_expectedverses"] = expected,
			    ["wa_wacsid"] = input.RepoId,
			    ["wa_user_id"] = input.User,
			    ["wa_repo_id"] = input.Repo,
			    ["wa_source_system"] = "WACS",
			    ["wa_language"] = language,
			    ["wa_url"] = repoUrl,
			    ["wa_name"] = $"WACS/{input.User}/{input.Repo}"
		    };
		    foreach (var (attribute, value) in analysisValues)
		    {
			    repo[attribute] = value;
		    }
		    await service.CreateAsync(repo);
	    }
    }

    /// <summary>
    /// Gets the GogsAnalysis fields to write to <c>wa_translationrepo</c>. Fields we don't have a value for are left out.
    /// Book, resource type, and level are left to GogsImporter.
    /// </summary>
    private async Task<Dictionary<string, object>> GetAnalysisValuesAsync(ComputedResult input, RepoAnalysisFields? analysis)
    {
	    var output = new Dictionary<string, object>();
	    if (analysis == null)
	    {
		    return output;
	    }

	    if (analysis.Translators != null)
	    {
		    output["wa_translators"] = analysis.Translators;
	    }
	    if (analysis.FinishedChunks != null)
	    {
		    output["wa_chunks_finished"] = analysis.FinishedChunks.Value;
	    }
	    if (analysis.UploadedChunks != null)
	    {
		    output["wa_chunks_uploaded"] = analysis.UploadedChunks.Value;
	    }
	    if (analysis.LastCommitDate != null)
	    {
		    output["wa_lastcommitdate"] = analysis.LastCommitDate.Value.ToUniversalTime();
	    }

	    if (analysis.BookId != null && analysis.ResourceType?.ToLowerInvariant() is "ulb" or "udb" or "reg")
	    {
		    try
		    {
			    var expectedChunks = await GetExpectedChunksAsync(input.LanguageCode, analysis.BookId);
			    if (expectedChunks != null)
			    {
				    output["wa_chunks_expected"] = expectedChunks.Value;
			    }
		    }
		    catch (Exception ex)
		    {
			    _log.LogWarning(ex, "Could not read expected chunks for {Book}", analysis.BookId);
		    }
	    }

	    return output;
    }

    /// <summary>
    /// Gets the number of chunks in a book from the chunk container BTTWriterCatalog maintains,
    /// preferring a language-specific chunk file over the default one.
    /// </summary>
    /// <returns>The chunk count, or null when no chunk file exists for the book.</returns>
    private async Task<int?> GetExpectedChunksAsync(string? languageCode, string bookId)
    {
	    using var activity = _activitySource.StartActivity();
	    var book = bookId.ToLowerInvariant();
	    var cacheKey = $"chunks:{languageCode}:{book}";
	    if (_cache.TryGetValue(cacheKey, out int? cached))
	    {
		    return cached;
	    }

	    var client = new BlobContainerClient(GetBlobConnectionString(), GetChunkContainerName());
	    BlobClient? source = null;
	    if (!string.IsNullOrWhiteSpace(languageCode))
	    {
		    var languageSpecific = client.GetBlobClient($"{languageCode}/{book}/chunks.json");
		    if (await languageSpecific.ExistsAsync())
		    {
			    source = languageSpecific;
		    }
	    }
	    if (source == null)
	    {
		    var defaultSource = client.GetBlobClient($"default/{book}/chunks.json");
		    if (await defaultSource.ExistsAsync())
		    {
			    source = defaultSource;
		    }
	    }

	    int? output = null;
	    if (source != null)
	    {
		    await using var stream = await source.OpenReadAsync();
		    using var document = await JsonDocument.ParseAsync(stream);
		    if (document.RootElement.ValueKind == JsonValueKind.Array)
		    {
			    output = document.RootElement.GetArrayLength();
		    }
	    }
	    else
	    {
		    _log.LogWarning("No chunk definition found for {Language} {Book}", languageCode, book);
	    }

	    _cache.Set(cacheKey, output, TimeSpan.FromMinutes(30));
	    return output;
    }

    private string GetChunkContainerName()
    {
	    return _config.GetValue("BlobStorageChunkContainer", "chunkscontainer")!;
    }

    /// <summary>
    /// Deletes the <c>wa_translationrepo</c> record(s) in PORT matching the given WACS repo ID.
    /// </summary>
    /// <remarks>
    /// <c>wa_wacsid</c> isn't the primary key, so we have to look up the Dataverse GUID before
    /// deleting. If no matching record exists this is a no-op.
    /// </remarks>
    /// <param name="service">Authenticated Dataverse service client.</param>
    /// <param name="repoId">The WACS repo ID (stored on the record as <c>wa_wacsid</c>).</param>
    private async Task DeleteFromPORT(ServiceClient service, int repoId)
    {
	    using var activity = _activitySource.StartActivity();
	    var query = new QueryExpression("wa_translationrepo")
	    {
		    ColumnSet = new ColumnSet()
	    };
	    query.Criteria.AddCondition("wa_wacsid", ConditionOperator.Equal, repoId);
	    var result = await service.RetrieveMultipleAsync(query);
	    foreach (var repo in result.Entities)
	    {
		    await service.DeleteAsync(repo.LogicalName, repo.Id);
	    }
    }

    private (int expected, int actual) CalculateTotalVerses(ComputedResult input)
    {
	    var actual = 0;
	    var expected = 0;

	    foreach (var book in input.Books)
	    {
		    if (book.IsEmpty)
		    {
			    continue;
		    }
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
		    var currentBook =
			    input.Books.FirstOrDefault(b => string.Equals(b.BookId, book, StringComparison.OrdinalIgnoreCase));
		    
		    outputBook.IsEmpty = currentBook == null;
		    outputBook.ActualChapters = currentBook == null ? 0 : currentBook.Chapters.Count;
		    
		    foreach (var (chapter, verseCounts) in bookCountDefinition.ExpectedChapterCounts)
		    {
			    var currentChapter = currentBook?.Chapters.FirstOrDefault(c => c.ChapterNumber == chapter);
			    var outputChapter = new ComputedResultChapter
			    {
				    Number = chapter,
				    ActualVerses = currentChapter?.VerseCount ?? 0,
				    ExpectedVerses = verseCounts,
				    BlankVerses = currentChapter?.BlankVerseCount ?? 0
			    };
			    outputBook.Chapters.Add(outputChapter);
		    }
	    }

	    return output;
    }
    
    private async Task<CountDefinitions> GetCountDefinitionsAsync(string languageCode)
    {
	    using var activity = _activitySource.StartActivity();
	    if ( _cache.TryGetValue(languageCode, out CountDefinitions? result))
	    {
		    if (result != null)
		    {
				return result;
		    }
	    }
	    var client = new BlobContainerClient(GetBlobConnectionString(), "versecounts");
	    var source = client.GetBlobClient($"{languageCode}.json");
	    if (!await source.ExistsAsync())
	    {
		    source = client.GetBlobClient("default.json");
	    }

	    var stream = await source.OpenReadAsync();
	    var output = await JsonSerializer.DeserializeAsync<CountDefinitions>(stream) ?? new CountDefinitions();
	    _cache.Set(languageCode, output, TimeSpan.FromMinutes(30));
	    return output;
    }
    private string? GetBlobConnectionString()
	{
		return _config.GetConnectionString("BlobStorage");
	}
}