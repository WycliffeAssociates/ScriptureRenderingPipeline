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
        /*
        [FunctionName("DatabaseTriggerTestFunction")]
        public static void Run([CosmosDBTrigger(
            databaseName: "BTTCatalog",
            collectionName: "Languages",
            ConnectionStringSetting = "DBConnectionString",
            CreateLeaseCollectionIfNotExists = true,
            LeaseCollectionName = "leases")]IReadOnlyList<Document> input, ILogger log)
        {
            var languages = input.Select(i => JsonConvert.DeserializeObject<Language>(i.ToString()));
            foreach(var language in languages)
            {
                log.LogInformation(language.Slug);
                log.LogInformation(language.Name);
                log.LogInformation(language.Direction);
            }
        }
        */
        [FunctionName("ManualCatalogGenerate")]
        public static async Task ManuallyGenerateCatalogAsync([HttpTrigger(authLevel: AuthorizationLevel.Anonymous, "post")] HttpRequest request, ILogger log)
        {
            var databaseConnectionString = Environment.GetEnvironmentVariable("DBConnectionString");
            var databaseName = Environment.GetEnvironmentVariable("DBName");
            var storageConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnectionString");
            var storageCatalogContainer = Environment.GetEnvironmentVariable("BlobStorageOutputContainer");
            var catalogBaseUrl = Environment.GetEnvironmentVariable("CatalogBaseUrl");
            var outputDir = Utils.CreateTempFolder();

            var cosmosClient = new CosmosClient(databaseConnectionString);
            Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
            Container resourcesDatabase = await database.CreateContainerIfNotExistsAsync("Resources", "/Parition");
            Container scriptureDatabase = await database.CreateContainerIfNotExistsAsync("Scripture", "/Parition");

            var allScriptureResources = new List<ScriptureResource>();

            log.LogInformation("Getting all scripture resources");
            var scriptureFeed = scriptureDatabase.GetItemQueryIterator<ScriptureResource>("select * from T");
            while (scriptureFeed.HasMoreResults)
            {
                allScriptureResources.AddRange(await scriptureFeed.ReadNextAsync());
            }


            log.LogInformation("Generating catalog");
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
                foreach (var project in allScriptureResources.Where(r => r.Book == book))
                {
                    var lastModifiedForBookAndLanguage = allScriptureResources.Where(r => r.Book == book && r.Language == project.Language).Select(r => r.ModifiedOn).Max();
                    log.LogInformation("Processing {language} {book}", project.Language, book);
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
                            name = project.Language,
                            direction = "ltr",
                        }
                    });
                    var projectsForLanguageAndBook = new List<CatalogResource>();
                    foreach(var languageProjects in allScriptureResources.Where(r => r.Book == book && r.Language == project.Language))
                    {
                        projectsForLanguageAndBook.Add(new CatalogResource()
                        {
                            checking_questions = "",
                            chunks = $"https://api.unfoldingword.org/bible/txt/1/{languageProjects.Book}/chunks.json",
                            date_modified = languageProjects.ModifiedOn.ToString("yyyyMMdd"),
                            notes = "",
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
                                publish_date = languageProjects.PublishedDate,
                            },
                            terms = "",
                            tw_cat = "",
                            usfm = "",
                        });
                        Directory.CreateDirectory(Path.Join(outputDir, "v2/ts/", book, "/", project.Language));
                        File.WriteAllText(Path.Join(outputDir, "v2/ts/", book, "/", project.Language,"/resources.json"), JsonConvert.SerializeObject(projectsForLanguageAndBook));
                    }
                }
                Directory.CreateDirectory(Path.Combine(outputDir, "v2/ts/", book));
                File.WriteAllText(Path.Combine(outputDir, "v2/ts/", book, "languages.json"), JsonConvert.SerializeObject(allProjectsForBook));
            }

            Directory.CreateDirectory(Path.Join(outputDir, "v2/ts"));
            File.WriteAllText(Path.Combine(outputDir, "v2/ts/catalog.json"),JsonConvert.SerializeObject(allBooks));

            /*
            var allTranslationNotes = new List<SupplimentalResources>();
            log.LogInformation("Getting all supplimental resources");
            var resourceFeed = resourcesDatabase.GetItemQueryIterator<SupplimentalResources>("select * from T");
            while (scriptureFeed.HasMoreResults)
            {
                allTranslationNotes.AddRange(await resourceFeed.ReadNextAsync());
            }
            */

            log.LogInformation("Uploading catalog files");
            Utils.UploadToStorage(log,storageConnectionString,storageCatalogContainer,outputDir,"");
        }
    }
}
