using BTTWriterCatalog.Helpers;
using BTTWriterCatalog.Models.DataModel;
using BTTWriterCatalog.Models.UnfoldingWordCatalog;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using PipelineCommon.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Azure;

namespace BTTWriterCatalog
{
    public static class UnfoldingWordCatalogGenerator
    {

        [FunctionName("UWCatalogManualBuild")]
        public static async Task<IActionResult> ManualBuildAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/UWCatalogManualBuild")] HttpRequest req, ILogger log)
        {
            await BuildCatalogAsync(log);
            return new OkResult();
        }
        
        [FunctionName("UWCatalogAutomaticBuild")]
        public static async Task TriggerFromDBAsync([CosmosDBTrigger(
            databaseName: "BTTWriterCatalog",
            containerName: "Scripture",
            Connection = "DBConnectionString",
            CreateLeaseContainerIfNotExists = true,
            LeaseContainerPrefix = "UWCatalog",
            LeaseContainerName = "leases")]IReadOnlyList<object> input, ILogger log)
        {
            await BuildCatalogAsync(log);
        }

        [FunctionName("UWCatalogAutomaticBuildFromDelete")]
        public static async Task TriggerFromDBDeleteAsync([CosmosDBTrigger(
            databaseName: "BTTWriterCatalog",
            containerName: "DeletedScripture",
            Connection = "DBConnectionString",
            CreateLeaseContainerIfNotExists = true,
            LeaseContainerPrefix = "UWCatalog",
            LeaseContainerName = "leases")]IReadOnlyList<object> input, ILogger log)
        {
            await BuildCatalogAsync(log);
        }

        private static async Task BuildCatalogAsync(ILogger log)
        {
            var databaseName = Environment.GetEnvironmentVariable("DBName");
            var storageConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnectionString");
            var storageCatalogContainer = Environment.GetEnvironmentVariable("BlobStorageOutputContainer");
            var catalogBaseUrl = Environment.GetEnvironmentVariable("CatalogBaseUrl")?.TrimEnd('/');

            var blobServiceClient = new BlobServiceClient(storageConnectionString);
            var container = blobServiceClient.GetBlobContainerClient(storageCatalogContainer);
            await container.CreateIfNotExistsAsync();
            var outputInterface = new DirectAzureUpload("uw/txt/2", container);

            var database = ConversionUtils.cosmosClient.GetDatabase(databaseName);
            var scriptureDatabase = database.GetContainer("Scripture");
            var output = new UnfoldingWordCatalogRoot();

            log.LogInformation("Getting all scripture resources");
            var allScriptureResources = await GetAllScriptureResourcesAsync(scriptureDatabase);
            // If there are any OBS resources build the OBS section
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
            // If there are any Scripture resources build the scripture section
            if (allScriptureResources.Any(b => b.Type != "obs"))
            {
                var bibleCatalog = new UnfoldingWordResource()
                {
                    Slug = "bible",
                    Title = "Bible",
                    Languages = CreateCatalogForResources(allScriptureResources.Where(i => i.Type != "obs"), catalogBaseUrl + "/bible/{0}/{1}/{2}/{2}.usfm"),
                };
                output.Catalog.Add(bibleCatalog);
            }

            await outputInterface.WriteAllTextAsync("catalog.json", JsonSerializer.Serialize(output, CatalogJsonContext.Default.UnfoldingWordCatalogRoot));
            await outputInterface.FinishAsync();
        }

        /// <summary>
        /// Create a catalog entry for catalog resources
        /// </summary>
        /// <param name="input">A list of scripture</param>
        /// <param name="urlFormatString">Format string to create the URL</param>
        /// <returns>A list of catalog entries</returns>
        private static List<UnfoldingWordLanguage> CreateCatalogForResources(IEnumerable<ScriptureResourceModel> input, string urlFormatString)
        {
            var output = new List<UnfoldingWordLanguage>();
            var bookIndexedByLanguage = new Dictionary<string, List<ScriptureResourceModel>>();
            foreach (var book in input)
            {
                if (!bookIndexedByLanguage.ContainsKey(book.Language))
                {
                    bookIndexedByLanguage.Add(book.Language, new List<ScriptureResourceModel>() { book });
                    continue;
                }

                bookIndexedByLanguage[book.Language].Add(book);
            }
            foreach (var (language, books) in bookIndexedByLanguage)
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
                foreach (var (type, indexedBook) in booksIndexedByType)
                {
                    var outputVersion = new UnfoldingWordVersion()
                    {
                        ModifiedOn = DateTimeToUnixTimestamp(indexedBook.Select(b => b.ModifiedOn).Max()).ToString(),
                        Name = indexedBook[0].Title,
                        Slug = $"{ indexedBook[0].Type}-{indexedBook[0].Language}",
                        Status = new UnfoldingWordStatus()
                        {
                            CheckingEntity = indexedBook[0].CheckingEntity,
                            CheckingLevel = indexedBook[0].CheckingLevel,
                            Comments = indexedBook[0].Comments,
                            Contributors = string.Join("; ", indexedBook[0].Contributors),
                            SourceText = indexedBook[0].SourceText,
                            SourceTextVersion = indexedBook[0].SourceTextVersion,
                            PublishDate = indexedBook[0].PublishedDate,
                        },
                        TableOfContents = indexedBook.Select(i => new UnfoldingWordTableOfContentsEntry()
                        {
                            Description = string.Empty,
                            ModifiedOn = DateTimeToUnixTimestamp(i.ModifiedOn).ToString(),
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
        /// <summary>
        /// Converts a datetime to a unix timestamp
        /// </summary>
        /// <param name="input">The Datetime to convert</param>
        /// <returns>A unix timestamp representing the input datetime</returns>
        private static long DateTimeToUnixTimestamp(DateTime input)
        {
            return ((DateTimeOffset)input).ToUnixTimeSeconds();
        }

        /// <summary>
        /// Get all scripture resources from the database
        /// </summary>
        /// <param name="scriptureDatabase"></param>
        /// <returns></returns>
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
    }
}
