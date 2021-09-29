using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BTTWriterCatalog.Models.DataModel;
using BTTWriterCatalog.Models.WriterCatalog;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PipelineCommon.Helpers;

namespace BTTWriterCatalog
{
    public static class WriterCatalogGenerator
    {
        [FunctionName("AutomaticallyUpdateFromScripture")]
        public static async Task AutomaticallyUpdateFromScriptureAsync([CosmosDBTrigger(
            databaseName: "BTTWriterCatalog",
            collectionName: "Scripture",
            ConnectionStringSetting = "DBConnectionString",
            CreateLeaseCollectionIfNotExists = true,
            LeaseCollectionPrefix = "WriterCatalog",
            LeaseCollectionName = "leases")]IReadOnlyList<Microsoft.Azure.Documents.Document> input, ILogger log)
        {
            var updatedScripture = input.Select(i => JsonConvert.DeserializeObject<ScriptureResourceModel>(i.ToString()));

            var databaseConnectionString = Environment.GetEnvironmentVariable("DBConnectionString");
            var databaseName = Environment.GetEnvironmentVariable("DBName");
            var storageConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnectionString");
            var storageCatalogContainer = Environment.GetEnvironmentVariable("BlobStorageOutputContainer");
            var catalogBaseUrl = Environment.GetEnvironmentVariable("CatalogBaseUrl");

            var cosmosClient = new CosmosClient(databaseConnectionString);
            Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
            Container resourcesDatabase = await database.CreateContainerIfNotExistsAsync("Resources", "/Parition");
            Container scriptureDatabase = await database.CreateContainerIfNotExistsAsync("Scripture", "/Parition");

            log.LogInformation("Getting all scripture resources");
            var allScriptureResources = await GetAllScriptureResources(scriptureDatabase);
            var allSupplimentalResources = await GetAllSupplimentalResources(resourcesDatabase);

            log.LogInformation("Generating catalog");
            var outputDir = Utils.CreateTempFolder();
            BuildCatalog(log, catalogBaseUrl, allScriptureResources, allSupplimentalResources, updatedScripture.Select(r => r.Language).ToList(), outputDir);

            log.LogInformation("Uploading catalog files");
            Utils.UploadToStorage(log, storageConnectionString, storageCatalogContainer, outputDir, "");
        }

        [FunctionName("AutomaticallyUpdateFromResources")]
        public static async Task AutomaticallyUpdateFromResourcesAsync([CosmosDBTrigger(
            databaseName: "BTTWriterCatalog",
            collectionName: "Resources",
            ConnectionStringSetting = "DBConnectionString",
            CreateLeaseCollectionIfNotExists = true,
            LeaseCollectionPrefix = "WriterCatalog",
            LeaseCollectionName = "leases")]IReadOnlyList<Microsoft.Azure.Documents.Document> input, ILogger log)
        {
            var updatedScripture = input.Select(i => JsonConvert.DeserializeObject<SupplimentalResourcesModel>(i.ToString()));
            var databaseConnectionString = Environment.GetEnvironmentVariable("DBConnectionString");
            var databaseName = Environment.GetEnvironmentVariable("DBName");
            var storageConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnectionString");
            var storageCatalogContainer = Environment.GetEnvironmentVariable("BlobStorageOutputContainer");
            var catalogBaseUrl = Environment.GetEnvironmentVariable("CatalogBaseUrl");

            var cosmosClient = new CosmosClient(databaseConnectionString);
            Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
            Container resourcesDatabase = await database.CreateContainerIfNotExistsAsync("Resources", "/Parition");
            Container scriptureDatabase = await database.CreateContainerIfNotExistsAsync("Scripture", "/Parition");

            log.LogInformation("Getting all scripture resources");
            var allScriptureResources = await GetAllScriptureResources(scriptureDatabase);
            var allSupplimentalResources = await GetAllSupplimentalResources(resourcesDatabase);

            log.LogInformation("Generating catalog");
            var outputDir = Utils.CreateTempFolder();
            BuildCatalog(log, catalogBaseUrl, allScriptureResources, allSupplimentalResources, updatedScripture.Select(r => r.Language).ToList(), outputDir);

            log.LogInformation("Uploading catalog files");
            Utils.UploadToStorage(log, storageConnectionString, storageCatalogContainer, outputDir, "");
        }

        [FunctionName("WriterCatalogManualBuild")]
        public static async Task ManuallyGenerateCatalogAsync([HttpTrigger(authLevel: AuthorizationLevel.Anonymous, "post")] HttpRequest request, ILogger log)
        {
            var databaseConnectionString = Environment.GetEnvironmentVariable("DBConnectionString");
            var databaseName = Environment.GetEnvironmentVariable("DBName");
            var storageConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnectionString");
            var storageCatalogContainer = Environment.GetEnvironmentVariable("BlobStorageOutputContainer");
            var catalogBaseUrl = Environment.GetEnvironmentVariable("CatalogBaseUrl");

            var cosmosClient = new CosmosClient(databaseConnectionString);
            Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
            Container resourcesDatabase = await database.CreateContainerIfNotExistsAsync("Resources", "/Parition");
            Container scriptureDatabase = await database.CreateContainerIfNotExistsAsync("Scripture", "/Parition");

            log.LogInformation("Getting all scripture resources");
            var allScriptureResources = await GetAllScriptureResources(scriptureDatabase);
            var allSupplimentalResources = await GetAllSupplimentalResources(resourcesDatabase);

            log.LogInformation("Generating catalog");
            var outputDir = Utils.CreateTempFolder();
            BuildCatalog(log, catalogBaseUrl, allScriptureResources, allSupplimentalResources, allScriptureResources.Select(r => r.Language).ToList(), outputDir);

            log.LogInformation("Uploading catalog files");
            Utils.UploadToStorage(log, storageConnectionString, storageCatalogContainer, outputDir, "");
        }

        private static void BuildCatalog(ILogger log, string catalogBaseUrl, List<ScriptureResourceModel> allScriptureResources, List<SupplimentalResourcesModel> allSupplimentalResources, List<string> languagesToUpdate, string outputDir)
        {
            var allBooks = new List<CatalogBook>();
            foreach (var book in allScriptureResources.Select(r => r.Book).Distinct())
            {
                var bookNumber = Utils.GetBookNumber(book);
                log.LogInformation("Processing {book}", book);
                var mostRecentModifiedOn = allScriptureResources.Where(r => r.Book == book).Select(r => r.ModifiedOn).Max();
                allBooks.Add(new CatalogBook()
                {
                    date_modified = mostRecentModifiedOn.ToString("yyyyMMdd"),
                    slug = book,
                    sort = bookNumber.ToString().PadLeft(2, '0'),
                    lang_catalog = Path.Join(catalogBaseUrl, "/v2/ts/", book, "/languages.json"),
                    meta = new string[] { bookNumber < 40 ? "bible-ot" : "bible-nt" }
                });
                var allProjectsForBook = new List<CatalogProject>();
                var processedLanguagesForThisBook = new List<string>();
                foreach (var project in allScriptureResources.Where(r => r.Book == book && languagesToUpdate.Contains(r.Language)))
                {
                    log.LogInformation("Processing {language} {project} {book}", project.Language, project.Identifier, book);
                    if (!processedLanguagesForThisBook.Contains(project.Language))
                    {
                        var lastModifiedForBookAndLanguage = allScriptureResources.Where(r => r.Book == book && r.Language == project.Language).Select(r => r.ModifiedOn).Max();
                        allProjectsForBook.Add(new CatalogProject()
                        {
                            res_catalog = Path.Join(catalogBaseUrl, "v2/ts/", book, "/", project.Language, "/resources.json"),
                            project = new Project()
                            {
                                name = project.BookName,
                                sort = bookNumber.ToString(),
                                desc = "",
                                meta = new string[] { bookNumber < 40 ? "bible-ot" : "bible-nt" }
                            },
                            language = new Models.WriterCatalog.Language()
                            {
                                date_modified = lastModifiedForBookAndLanguage.ToString("yyyyMMdd"),
                                slug = project.Language,
                                name = project.LanguageName,
                                direction = "ltr",
                            }
                        });
                        processedLanguagesForThisBook.Add(project.Language);
                    }
                    var projectsForLanguageAndBook = new List<CatalogResource>();
                    foreach (var languageProjects in allScriptureResources.Where(r => r.Book == book && r.Language == project.Language))
                    {
                        projectsForLanguageAndBook.Add(new CatalogResource()
                        {
                            checking_questions = allSupplimentalResources.Any(r => r.Book == book && r.Language == project.Language && r.ResourceType == "tq") ? $"{catalogBaseUrl}/tq/{project.Language}/{book}/questions.json" : "",
                            chunks = $"{catalogBaseUrl}/bible/{languageProjects.Language}/{languageProjects.Identifier}/{book}/chunks.json",
                            date_modified = languageProjects.ModifiedOn.ToString("yyyyMMdd"),
                            notes = allSupplimentalResources.Any(r => r.Book == book && r.Language == project.Language && r.ResourceType == "tn") ? $"{catalogBaseUrl}/tn/{project.Language}/{book}/notes.json" : "",
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
                            terms = allSupplimentalResources.Any(r => r.Book == book && r.Language == project.Language && r.ResourceType == "tw") ? $"{catalogBaseUrl}/tw/{project.Language}/words.json" : "",
                            tw_cat = allSupplimentalResources.Any(r => r.Book == book && r.Language == project.Language && r.ResourceType == "tw_cat") ? $"{catalogBaseUrl}/tw/{project.Language}/{book.ToLower()}/tw_cat.json" : string.Empty,
                            usfm = $"{catalogBaseUrl}/bible/{languageProjects.Language}/{languageProjects.Identifier}/{book}/source.usfm",
                        });
                        Directory.CreateDirectory(Path.Join(outputDir, "v2/ts/", book, "/", project.Language));
                        File.WriteAllText(Path.Join(outputDir, "v2/ts/", book, "/", project.Language, "/resources.json"), JsonConvert.SerializeObject(projectsForLanguageAndBook));
                    }
                }
                Directory.CreateDirectory(Path.Combine(outputDir, "v2/ts/", book));
                File.WriteAllText(Path.Combine(outputDir, "v2/ts/", book, "languages.json"), JsonConvert.SerializeObject(allProjectsForBook));
            }

            Directory.CreateDirectory(Path.Join(outputDir, "v2/ts"));
            File.WriteAllText(Path.Combine(outputDir, "v2/ts/catalog.json"), JsonConvert.SerializeObject(allBooks));
        }

        private static async Task<List<ScriptureResourceModel>> GetAllScriptureResources(Container scriptureDatabase)
        {
            var output = new List<ScriptureResourceModel>();
            var feed = scriptureDatabase.GetItemQueryIterator<ScriptureResourceModel>("select * from T");
            while (feed.HasMoreResults)
            {
                output.AddRange(await feed.ReadNextAsync());
            }
            return output;
        }
        private static async Task<List<ScriptureResourceModel>> GetScriptureResourcesForLanguage(Container scriptureDatabase, string language)
        {
            var output = new List<ScriptureResourceModel>();
            var feed = scriptureDatabase.GetItemQueryIterator<ScriptureResourceModel>(new QueryDefinition("select * from T where T.Language = @Language").WithParameter("Language", language));
            while (feed.HasMoreResults)
            {
                output.AddRange(await feed.ReadNextAsync());
            }
            return output;
        }
        private static async Task<List<SupplimentalResourcesModel>> GetAllSupplimentalResources(Container database)
        {
            var output = new List<SupplimentalResourcesModel>();
            var feed = database.GetItemQueryIterator<SupplimentalResourcesModel>("select * from T");
            while (feed.HasMoreResults)
            {
                output.AddRange(await feed.ReadNextAsync());
            }
            return output;
        }
    }
}