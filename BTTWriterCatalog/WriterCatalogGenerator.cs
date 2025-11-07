using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using BTTWriterCatalog.Models.DataModel;
using BTTWriterCatalog.Models.WriterCatalog;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using PipelineCommon.Helpers;

namespace BTTWriterCatalog
{
    public class WriterCatalogGenerator
    {
        private readonly ILogger<WriterCatalogGenerator> _log;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly CosmosClient _cosmosClient;
        private readonly BlobContainerClient _outputContainerClient;
        private readonly string _databaseName;
        private readonly string _catalogBaseUrl;
        const string TopicName = "CatalogGenerated";

        public WriterCatalogGenerator(ILogger<WriterCatalogGenerator> logger, CosmosClient cosmosClient,
            IAzureClientFactory<BlobServiceClient> blobServiceClientFactory,
            IAzureClientFactory<ServiceBusClient> serviceBusClientFactory,
            IConfiguration configuration)
        {
            _log = logger;
            _cosmosClient = cosmosClient;
            _serviceBusClient = serviceBusClientFactory.CreateClient("ServiceBusClient");
            var blobServiceClient = blobServiceClientFactory.CreateClient("BlobServiceClient");
            _outputContainerClient = blobServiceClient.GetBlobContainerClient(configuration.GetValue<string>("BlobStorageOutputContainer"));
            _databaseName = configuration.GetValue<string>("DBName");
            _catalogBaseUrl = configuration.GetValue<string>("CatalogBaseUrl")?.TrimEnd('/');
        }
        
        [Function("AutomaticallyUpdateFromScripture")]
        public async Task AutomaticallyUpdateFromScriptureAsync([CosmosDBTrigger(
            databaseName: "BTTWriterCatalog",
            containerName: "Scripture",
            Connection = "DBConnectionString",
            CreateLeaseContainerIfNotExists = true,
            LeaseContainerPrefix = "WriterCatalog",
            LeaseContainerName = "leases")] IEnumerable <ScriptureResourceModel> input)
        {
            await BuildCatalogAsync(input.Select(r => r.Language).Distinct().ToList());
        }

        [Function("AutomaticallyUpdateFromResources")]
        public async Task AutomaticallyUpdateFromResourcesAsync([CosmosDBTrigger(
            databaseName: "BTTWriterCatalog",
            containerName: "Resources",
            Connection = "DBConnectionString",
            CreateLeaseContainerIfNotExists = true,
            LeaseContainerPrefix = "WriterCatalog",
            LeaseContainerName = "leases")]IReadOnlyList<SupplementalResourcesModel> input)
        {
            await BuildCatalogAsync(input.Select(r => r.Language).Distinct().ToList());
        }
        [Function("AutomaticallyUpdateFromScriptureDelete")]
        public async Task AutomaticallyUpdateFromScriptureDeleteAsync([CosmosDBTrigger(
            databaseName: "BTTWriterCatalog",
            containerName: "DeletedScripture",
            Connection = "DBConnectionString",
            CreateLeaseContainerIfNotExists = true,
            LeaseContainerPrefix = "WriterCatalog",
            LeaseContainerName = "leases")]IReadOnlyList<ScriptureResourceModel> input)
        {
            await BuildCatalogAsync(input.Select(r => r.Language).Distinct().ToList());
        }

        [Function("AutomaticallyUpdateFromResourcesDelete")]
        public async Task AutomaticallyUpdateFromResourcesDeleteAsync([CosmosDBTrigger(
            databaseName: "BTTWriterCatalog",
            containerName: "DeletedResources",
            Connection = "DBConnectionString",
            CreateLeaseContainerIfNotExists = true,
            LeaseContainerPrefix = "WriterCatalog",
            LeaseContainerName = "leases")]IReadOnlyList<SupplementalResourcesModel> input)
        {
            await BuildCatalogAsync(input.Select(r => r.Language).Distinct().ToList());
        }

        [Function("WriterCatalogManualBuild")]
        public async Task ManuallyGenerateCatalogAsync([HttpTrigger(authLevel: AuthorizationLevel.Anonymous, "post", Route = "api/WriterCatalogManualBuild")] HttpRequest request)
        {
            await BuildCatalogAsync();
        }

        /// <summary>
        /// Main catalog generation function
        /// </summary>
        /// <param name="languagesToUpdate">A list of languages to do a delta update on, if it is null it will process everything</param>
        /// <returns>Nothing</returns>
        private async Task BuildCatalogAsync(List<string> languagesToUpdate = null)
        {
            // Ensure output container exists
            await _outputContainerClient.CreateIfNotExistsAsync();
            var outputInterface = new DirectAzureUpload("v2/ts/", _outputContainerClient);

            var database = _cosmosClient.GetDatabase(_databaseName);
            var resourcesDatabase = database.GetContainer("Resources");
            var scriptureDatabase = database.GetContainer("Scripture");

            _log.LogInformation("Getting all scripture resources");
            var allScriptureResources = await GetAllScriptureResourcesAsync(scriptureDatabase);
            var allSupplementalResources = await GetAllSupplementalResourcesAsync(resourcesDatabase);
            
            languagesToUpdate ??= allScriptureResources.Select(r => r.Language).ToList();

            _log.LogInformation("Generating catalog");

            var allBooks = new List<WriterCatalogBook>();
            var writingTasks = new List<Task>();
            var scriptureResourcesIndexedByBook = allScriptureResources.GroupBy(r => r.Book).ToDictionary( r => r.Key, r => r.ToList());
            var supplementalResourcesIndexedByBook = allSupplementalResources.GroupBy(r => r.Book).ToDictionary( r => r.Key, r => r.ToList());
            _log.LogInformation("Finished grouping resources");
            // Loop though all books and build the main catalog.json
            foreach (var (book,projects) in scriptureResourcesIndexedByBook)
            {
                var bookNumber = Utils.GetBookNumber(book);
                _log.LogInformation("Processing {Book}", book);
                var mostRecentModifiedOn = scriptureResourcesIndexedByBook[book].Select(r => r.ModifiedOn).Max();
                allBooks.Add(new WriterCatalogBook()
                {
                    date_modified = mostRecentModifiedOn.ToString("yyyyMMdd"),
                    slug = book,
                    sort = bookNumber.ToString().PadLeft(2, '0'),
                    lang_catalog = $"{_catalogBaseUrl}/v2/ts/{book}/languages.json",
                    meta = [bookNumber < 40 ? "bible-ot" : "bible-nt"]
                });

                var allProjectsForBook = new List<WriterCatalogProject>();
                var processedLanguagesForThisBook = new HashSet<string>();
                
                // Loop through all the projects for this book
                foreach (var project in projects)
                {
                    if (processedLanguagesForThisBook.Contains(project.Language))
                    {
                        continue;
                    }
                    
                    _log.LogInformation("Processing {Language} {Book}", project.Language, book);
                    var lastModifiedForBookAndLanguage = projects.Select(r => r.ModifiedOn).Max();
                    allProjectsForBook.Add(CreateWriterCatalogItem(_catalogBaseUrl, book, project, bookNumber, lastModifiedForBookAndLanguage));
                    var projectsForLanguageAndBook = new List<WriterCatalogResource>();

                    // If this was one of the requested languages to update then build the resources.json
                    if (languagesToUpdate.Contains(project.Language))
                    {
                        foreach (var languageProjects in projects)
                        {
                            if (languageProjects.Language != project.Language)
                            {
                                continue;
                            }

                            var hasCheckingQuestions = false;
                            var hasNotes = false;
                            var hasTranslationWords = false;
                            var hasTranslationWordsCatalog = false;
                            if (supplementalResourcesIndexedByBook.ContainsKey(book))
                            {
                                foreach(var resource in supplementalResourcesIndexedByBook[book].Where(r => r.Language == project.Language))
                                {
                                    switch (resource.ResourceType)
                                    {
                                        case "tq":
                                            hasCheckingQuestions = true;
                                            break;
                                        case "tn":
                                            hasNotes = true;
                                            break;
                                        case "tw":
                                            hasTranslationWords = true;
                                            break;
                                        case "tw_cat":
                                            hasTranslationWordsCatalog = true;
                                            break;
                                    }
                                }
                            }
                            _log.LogDebug("Processing {Language} {Project} {Book}", project.Language, project.Identifier, book);
                            projectsForLanguageAndBook.Add(new WriterCatalogResource()
                            {
                                checking_questions = hasCheckingQuestions ? $"{_catalogBaseUrl}/tq/{project.Language}/{book}/questions.json" : "",
                                chunks = $"{_catalogBaseUrl}/bible/{languageProjects.Language}/{languageProjects.Identifier}/{book}/chunks.json",
                                date_modified = languageProjects.ModifiedOn.ToString("yyyyMMdd"),
                                notes = hasNotes ? $"{_catalogBaseUrl}/tn/{project.Language}/{book}/notes.json" : "",
                                slug = languageProjects.Identifier,
                                source = $"{_catalogBaseUrl}/bible/{languageProjects.Language}/{languageProjects.Identifier}/{book}/source.json",
                                name = languageProjects.Title,
                                status = new Status()
                                {
                                    checking_entity = languageProjects.CheckingEntity,
                                    checking_level = languageProjects.CheckingLevel,
                                    contributors = string.Join(", ", languageProjects.Contributors),
                                    source_text = languageProjects.SourceText,
                                    source_text_version = languageProjects.SourceTextVersion,
                                    version = languageProjects.Version,
                                    publish_date = languageProjects.ModifiedOn,
                                },
                                terms = hasTranslationWords ? $"{_catalogBaseUrl}/tw/{project.Language}/words.json" : "",
                                tw_cat = hasTranslationWordsCatalog ? $"{_catalogBaseUrl}/tw/{project.Language}/{book.ToLower()}/tw_cat.json" : string.Empty,
                                usfm = $"{_catalogBaseUrl}/bible/{languageProjects.Language}/{languageProjects.Identifier}/{book}/{book}.usfm",
                            });
                        }
                        outputInterface.CreateDirectory(Path.Join(book, "/", project.Language));
                        writingTasks.Add(outputInterface.WriteAllTextAsync(Path.Join(book, "/", project.Language, "/resources.json"), JsonSerializer.Serialize(projectsForLanguageAndBook, CatalogJsonContext.Default.ListWriterCatalogResource)));
                    }

                    processedLanguagesForThisBook.Add(project.Language);
                }
                outputInterface.CreateDirectory(book);
                writingTasks.Add(outputInterface.WriteAllTextAsync(Path.Combine(book, "languages.json"), JsonSerializer.Serialize(allProjectsForBook, CatalogJsonContext.Default.ListWriterCatalogProject)));
            }

            outputInterface.CreateDirectory("v2/ts");
            writingTasks.Add(outputInterface.WriteAllTextAsync("catalog.json", JsonSerializer.Serialize(allBooks, CatalogJsonContext.Default.ListWriterCatalogBook)));

            // Wait for all the files to be written out to the filesystem
            writingTasks.Add(outputInterface.FinishAsync());
            await Task.WhenAll(writingTasks);

            _log.LogInformation("Checking to see if we need to delete any blobs");
            // TODO: Move delete to timed job
            // Figure out if anything needs to be removed from storage
            await CleanUpStorage(_log, languagesToUpdate, allScriptureResources, _outputContainerClient);
            await SendCompletedMessageAsync();
        }

        private static async Task CleanUpStorage(ILogger log, List<string> languagesToUpdate, List<ScriptureResourceModel> allScriptureResources,
            BlobContainerClient container)
        {
            var bibleBooks = Utils.BibleBookOrder.Select(b => b.ToLower());
            foreach (var language in languagesToUpdate)
            {
                foreach (var book in bibleBooks)
                {
                    if (!allScriptureResources.Any(r => r.Language == language && r.Book == book))
                    {
                        var blobPath = $"v2/ts/{book}/{language}/resources.json";
                        log.LogDebug("Deleting {Blob}", blobPath);
                        await container.DeleteBlobIfExistsAsync(blobPath);
                    }
                }
            }
        }

        private static WriterCatalogProject CreateWriterCatalogItem(string catalogBaseUrl, string book, ScriptureResourceModel project, int bookNumber, DateTime lastModifiedForBookAndLanguage)
        {
            return new WriterCatalogProject()
            {
                res_catalog = $"{catalogBaseUrl}/v2/ts/{book}/{project.Language}/resources.json",
                project = new Project()
                {
                    name = project.BookName,
                    sort = bookNumber.ToString(),
                    desc = "",
                    meta = [bookNumber < 40 ? "bible-ot" : "bible-nt"]
                },
                language = new Language()
                {
                    date_modified = lastModifiedForBookAndLanguage.ToString("yyyyMMdd"),
                    slug = project.Language,
                    name = project.LanguageName,
                    direction = project.LanguageDirection,
                }
            };
        }

        private static async Task<List<ScriptureResourceModel>> GetAllScriptureResourcesAsync(Container scriptureDatabase)
        {
            var output = new List<ScriptureResourceModel>();
            var feed = scriptureDatabase.GetItemQueryIterator<ScriptureResourceModel>("select * from T");
            while (feed.HasMoreResults)
            {
                output.AddRange(await feed.ReadNextAsync());
            }
            return output;
        }

        private static async Task<List<SupplementalResourcesModel>> GetAllSupplementalResourcesAsync(Container database)
        {
            var output = new List<SupplementalResourcesModel>();
            var feed = database.GetItemQueryIterator<SupplementalResourcesModel>("select * from T");
            while (feed.HasMoreResults)
            {
                output.AddRange(await feed.ReadNextAsync());
            }
            return output;
        }

        private async Task SendCompletedMessageAsync()
        {
            await using var sender = _serviceBusClient.CreateSender(TopicName);
            // Send a notification that the catalog has been updated. And yes it is ok that it is blank
            await sender.SendMessageAsync(new ServiceBusMessage());
        }
    }
}
