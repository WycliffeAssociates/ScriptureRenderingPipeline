using BTTWriterCatalog.Helpers;
using BTTWriterCatalog.Models.DataModel;
using BTTWriterCatalog.Models.UnfoldingWordCatalog;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PipelineCommon.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTTWriterCatalog
{
    public static class UnfoldingWordCatalogGenerator
    {

        [FunctionName("UWCatalogManualBuild")]
        public static async Task<IActionResult> ManualBuild([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/UWCatalogManualBuild")] HttpRequest req, ILogger log)
        {
            await BuildCatalogAsync(log);
            return new OkResult();
        }
        [FunctionName("UWCatalogAutomaticBuild")]
        public static async Task TriggerFromDBAsync([CosmosDBTrigger(
            databaseName: "BTTWriterCatalog",
            collectionName: "Scripture",
            ConnectionStringSetting = "DBConnectionString",
            CreateLeaseCollectionIfNotExists = true,
            LeaseCollectionPrefix = "UWCatalog",
            LeaseCollectionName = "leases")]IReadOnlyList<Microsoft.Azure.Documents.Document> input, ILogger log)
        {
            await BuildCatalogAsync(log);
        }

        [FunctionName("UWCatalogAutomaticBuildFromDelete")]
        public static async Task TriggerFromDBDeleteAsync([CosmosDBTrigger(
            databaseName: "BTTWriterCatalog",
            collectionName: "DeletedScripture",
            ConnectionStringSetting = "DBConnectionString",
            CreateLeaseCollectionIfNotExists = true,
            LeaseCollectionPrefix = "UWCatalog",
            LeaseCollectionName = "leases")]IReadOnlyList<Microsoft.Azure.Documents.Document> input, ILogger log)
        {
            await BuildCatalogAsync(log);
        }

        private static async Task BuildCatalogAsync(ILogger log)
        {
            var databaseConnectionString = Environment.GetEnvironmentVariable("DBConnectionString");
            var databaseName = Environment.GetEnvironmentVariable("DBName");
            var storageConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnectionString");
            var storageCatalogContainer = Environment.GetEnvironmentVariable("BlobStorageOutputContainer");
            var catalogBaseUrl = Environment.GetEnvironmentVariable("CatalogBaseUrl");
            var outputDir = Utils.CreateTempFolder();

            Database database = ConversionUtils.cosmosClient.GetDatabase(databaseName);
            Container scriptureDatabase = database.GetContainer("Scripture");
            var output = new UnfoldingWordCatalogRoot();

            log.LogInformation("Getting all scripture resources");
            var allScriptureResources = await GetAllScriptureResources(scriptureDatabase);
            if (allScriptureResources.Any(i => i.Type == "obs"))
            {
                var obsCatalog = new UnfoldingWordResource()
                {
                    Slug = "obs",
                    Title = "Open Bible Stories",
                    Languages = CreateCatalogForResources(allScriptureResources.Where(i => i.Type == "obs"), ""),
                };
                output.Catalog.Add(obsCatalog);
            }
            if (allScriptureResources.Any(b => b.Type != "obs"))
            {
                var bibleCatalog = new UnfoldingWordResource()
                {
                    Slug = "bible",
                    Title = "Bible",
                    Languages = CreateCatalogForResources(allScriptureResources.Where(i => i.Type != "obs"), catalogBaseUrl + "/bible/{0}/{1}/{2}/source.usfm"),
                };
                output.Catalog.Add(bibleCatalog);
            }

            File.WriteAllText(Path.Join(outputDir, "catalog.json"), JsonConvert.SerializeObject(output));
            await Utils.UploadToStorage(log, storageConnectionString, storageCatalogContainer, outputDir, "uw/txt/2");
            Directory.Delete(outputDir, true);
        }

        private static List<UnfoldingWordLanguage> CreateCatalogForResources(IEnumerable<ScriptureResourceModel> input, string urlFormatString)
        {
            // TODO: Come back to this to clean up some of the variables
            var output = new List<UnfoldingWordLanguage>();
            var bookIndexdByLanguage = new Dictionary<string, List<ScriptureResourceModel>>();
            foreach (var book in input)
            {
                if (!bookIndexdByLanguage.ContainsKey(book.Language))
                {
                    bookIndexdByLanguage.Add(book.Language, new List<ScriptureResourceModel>() { book });
                    continue;
                }

                bookIndexdByLanguage[book.Language].Add(book);
            }
            foreach (var (language, books) in bookIndexdByLanguage)
            {
                var outputLanguage = new UnfoldingWordLanguage()
                {
                    Language = language,
                    ModifiedOn = DateTimeToUnixTimestamp(books.Select(b => b.ModifiedOn).Max()).ToString(),
                };
                var booksIndexedByType = new Dictionary<string, List<ScriptureResourceModel>>();
                foreach (var book in books)
                {
                    if (!booksIndexedByType.ContainsKey(book.Type))
                    {
                        booksIndexedByType.Add(book.Type, new List<ScriptureResourceModel>() { book });
                        continue;
                    }
                    booksIndexedByType[book.Type].Add(book);
                }
                foreach (var (type, b) in booksIndexedByType)
                {
                    var outputVersion = new UnfoldingWordVersion()
                    {
                        ModifiedOn = DateTimeToUnixTimestamp(b.Select(b => b.ModifiedOn).Max()).ToString(),
                        Name = b[0].Title,
                        Slug = $"{ b[0].Type}-{b[0].Language}",
                        Status = new UnfoldingWordStatus()
                        {
                            CheckingEntity = b[0].CheckingEntity,
                            CheckingLevel = b[0].CheckingLevel,
                            Comments = b[0].Comments,
                            Contributors = string.Join("; ", b[0].Contributors),
                            SourceText = b[0].SourceText,
                            SourceTextVersion = b[0].SourceTextVersion,
                            PublishDate = b[0].PublishedDate,
                        },
                        TableOfContents = b.Select(i => new UnfoldingWordTableOfContentsEntry()
                        {
                            Description = string.Empty,
                            ModifiedOn = i.ModifiedOn.Ticks.ToString(),
                            Source = string.Format(urlFormatString, language, i.Type, i.Book),
                            SourceSignature = string.Empty,
                            Slug = i.Book,
                            Title = i.BookName
                        }).ToList(),
                    };
                    outputLanguage.Versions.Add(outputVersion);
                }
                output.Add(outputLanguage);
            }
            return output;
        }
        private static long DateTimeToUnixTimestamp(DateTime input)
        {
            return ((DateTimeOffset)input).ToUnixTimeSeconds();
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
    }
}
