using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using BTTWriterCatalog.Helpers;
using BTTWriterCatalog.Models.DataModel;
using BTTWriterCatalog.Models.WriterCatalog;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using PipelineCommon.Helpers;

namespace BTTWriterCatalog
{
    public class WriterCatalogGenerator
    {
        private ILogger<WriterCatalogGenerator> log;
        private BlobServiceClient blobClient;
        public WriterCatalogGenerator(ILogger<WriterCatalogGenerator> logger, IAzureClientFactory<BlobServiceClient> blobClientFactory)
        {
            log = logger;
            blobClient = blobClientFactory.CreateClient("BlobStorageClient");
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
            await BuildCatalogAsync(log, input.Select(r => r.Language).Distinct().ToList());
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
            await BuildCatalogAsync(log, input.Select(r => r.Language).Distinct().ToList());
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
            await BuildCatalogAsync(log, input.Select(r => r.Language).Distinct().ToList());
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
            await BuildCatalogAsync(log, input.Select(r => r.Language).Distinct().ToList());
        }

        [Function("WriterCatalogManualBuild")]
        public  async Task ManuallyGenerateCatalogAsync([HttpTrigger(authLevel: AuthorizationLevel.Anonymous, "post", Route = "api/WriterCatalogManualBuild")] HttpRequest request)
        {
            await BuildCatalogAsync(log);
        }

        /// <summary>
        /// Main catalog generation function
        /// </summary>
        /// <param name="log">An instance of ILogger</param>
        /// <param name="languagesToUpdate">A list of languages to do a delta update on, if it is null it will process everything</param>
        /// <returns>Nothing</returns>
        private static async Task BuildCatalogAsync(ILogger log, List<string> languagesToUpdate = null)
        {
            var databaseName = Environment.GetEnvironmentVariable("DBName");
            var storageConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnectionString");
            var storageCatalogContainer = Environment.GetEnvironmentVariable("BlobStorageOutputContainer");
            var catalogBaseUrl = Environment.GetEnvironmentVariable("CatalogBaseUrl");

            var outputDir = Utils.CreateTempFolder();

            var database = ConversionUtils.cosmosClient.GetDatabase(databaseName);
            var resourcesDatabase = database.GetContainer("Resources");
            var scriptureDatabase = database.GetContainer("Scripture");

            log.LogInformation("Getting all scripture resources");
            var allScriptureResources = await GetAllScriptureResourcesAsync(scriptureDatabase);
            var allSupplementalResources = await GetAllSupplementalResourcesAsync(resourcesDatabase);
            
            languagesToUpdate ??= allScriptureResources.Select(r => r.Language).ToList();

            log.LogInformation("Generating catalog");

            var allBooks = new List<WriterCatalogBook>();
            var writingTasks = new List<Task>();
            // Loop though all books and build the main catalog.json
            foreach (var book in allScriptureResources.Select(r => r.Book).Distinct())
            {
                var bookNumber = Utils.GetBookNumber(book);
                log.LogDebug("Processing {book}", book);
                var mostRecentModifiedOn = allScriptureResources.Where(r => r.Book == book).Select(r => r.ModifiedOn).Max();
                allBooks.Add(new WriterCatalogBook()
                {
                    date_modified = mostRecentModifiedOn.ToString("yyyyMMdd"),
                    slug = book,
                    sort = bookNumber.ToString().PadLeft(2, '0'),
                    lang_catalog = Path.Join(catalogBaseUrl, "/v2/ts/", book, "/languages.json"),
                    meta = new string[] { bookNumber < 40 ? "bible-ot" : "bible-nt" }
                });

                var allProjectsForBook = new List<WriterCatalogProject>();
                var processedLanguagesForThisBook = new List<string>();
                // Loop through languages for this book and build the languages.json
                foreach (var project in allScriptureResources)
                {
                    if (project.Book != book)
                    {
                        continue;
                    }
                    if (!processedLanguagesForThisBook.Contains(project.Language))
                    {
                        log.LogDebug("Processing {language} {book}", project.Language, book);
                        var lastModifiedForBookAndLanguage = allScriptureResources.Where(r => r.Book == book && r.Language == project.Language).Select(r => r.ModifiedOn).Max();
                        allProjectsForBook.Add(new WriterCatalogProject()
                        {
                            res_catalog = Path.Join(catalogBaseUrl, "v2/ts/", book, "/", project.Language, "/resources.json"),
                            project = new Project()
                            {
                                name = project.BookName,
                                sort = bookNumber.ToString(),
                                desc = "",
                                meta = new string[] { bookNumber < 40 ? "bible-ot" : "bible-nt" }
                            },
                            language = new Language()
                            {
                                date_modified = lastModifiedForBookAndLanguage.ToString("yyyyMMdd"),
                                slug = project.Language,
                                name = project.LanguageName,
                                direction = project.LanguageDirection,
                            }
                        });
                        var projectsForLanguageAndBook = new List<WriterCatalogResource>();

                        // If this was one of the requested languages to update then build the resources.json
                        if (languagesToUpdate.Contains(project.Language))
                        {
                            foreach (var languageProjects in allScriptureResources)
                            {
                                if (languageProjects.Book != book || languageProjects.Language != project.Language)
                                {
                                    continue;
                                }
                                log.LogDebug("Processing {language} {project} {book}", project.Language, project.Identifier, book);
                                projectsForLanguageAndBook.Add(new WriterCatalogResource()
                                {
                                    checking_questions = allSupplementalResources.Any(r => r.Book == book && r.Language == project.Language && r.ResourceType == "tq") ? $"{catalogBaseUrl}/tq/{project.Language}/{book}/questions.json" : "",
                                    chunks = $"{catalogBaseUrl}/bible/{languageProjects.Language}/{languageProjects.Identifier}/{book}/chunks.json",
                                    date_modified = languageProjects.ModifiedOn.ToString("yyyyMMdd"),
                                    notes = allSupplementalResources.Any(r => r.Book == book && r.Language == project.Language && r.ResourceType == "tn") ? $"{catalogBaseUrl}/tn/{project.Language}/{book}/notes.json" : "",
                                    slug = languageProjects.Identifier,
                                    source = $"{catalogBaseUrl}/bible/{languageProjects.Language}/{languageProjects.Identifier}/{book}/source.json",
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
                                    terms = allSupplementalResources.Any(r => r.Book == book && r.Language == project.Language && r.ResourceType == "tw") ? $"{catalogBaseUrl}/tw/{project.Language}/words.json" : "",
                                    tw_cat = allSupplementalResources.Any(r => r.Book == book && r.Language == project.Language && r.ResourceType == "tw_cat") ? $"{catalogBaseUrl}/tw/{project.Language}/{book.ToLower()}/tw_cat.json" : string.Empty,
                                    usfm = $"{catalogBaseUrl}/bible/{languageProjects.Language}/{languageProjects.Identifier}/{book}/{book}.usfm",
                                });
                            }
                            Directory.CreateDirectory(Path.Join(outputDir, "v2/ts/", book, "/", project.Language));
                            writingTasks.Add(File.WriteAllTextAsync(Path.Join(outputDir, "v2/ts/", book, "/", project.Language, "/resources.json"), JsonSerializer.Serialize(projectsForLanguageAndBook, CatalogJsonContext.Default.ListWriterCatalogResource)));
                        }

                        processedLanguagesForThisBook.Add(project.Language);
                    }
                }
                Directory.CreateDirectory(Path.Combine(outputDir, "v2/ts/", book));
                writingTasks.Add(File.WriteAllTextAsync(Path.Combine(outputDir, "v2/ts/", book, "languages.json"), JsonSerializer.Serialize(allProjectsForBook, CatalogJsonContext.Default.ListWriterCatalogProject)));
            }

            Directory.CreateDirectory(Path.Join(outputDir, "v2/ts"));
            writingTasks.Add(File.WriteAllTextAsync(Path.Combine(outputDir, "v2/ts/catalog.json"), JsonSerializer.Serialize(allBooks, CatalogJsonContext.Default.ListWriterCatalogBook)));

            // Wait for all of the files to be written out to the filesystem
            await Task.WhenAll(writingTasks);

            log.LogInformation("Uploading catalog files");
            var uploadTask = CloudStorageUtils.UploadToStorage(log, storageConnectionString, storageCatalogContainer, outputDir, "");


            log.LogInformation("Checking to see if we need to delete any blobs");
            // TODO: Move delete to timed job
            // Figure out if anything needs to be removed from storage
            var outputClient = new BlobContainerClient(storageConnectionString, storageCatalogContainer);
            foreach (var language in languagesToUpdate)
            {
                foreach (var book in Utils.BibleBookOrder.Select(b => b.ToLower()))
                {
                    if (!allScriptureResources.Any(r => r.Language == language && r.Book == book))
                    {
                        var blobPath = $"v2/ts/{book}/{language}/resources.json";
                        log.LogDebug("Deleting {blob}", blobPath);
                        await outputClient.DeleteBlobIfExistsAsync(blobPath);
                    }
                }
            }

            await uploadTask;

            Directory.Delete(outputDir, true);
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
    }
}
