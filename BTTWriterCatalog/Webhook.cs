using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Data.Tables;
using Microsoft.Azure.Cosmos;
using BTTWriterCatalog.Models.DataModel;
using PipelineCommon.Models.Webhook;
using PipelineCommon.Helpers;
using System.Net;
using YamlDotNet.Serialization;
using PipelineCommon.Models.ResourceContainer;
using System.Collections.Generic;
using BTTWriterCatalog.Models;
using Azure.Storage.Blobs;
using BTTWriterCatalog.ContentConverters;
using System.Linq;
using USFMToolsSharp.Models.Markers;
using USFMToolsSharp;
using BTTWriterCatalog.Helpers;
using System.Net.Http;
using Azure.Storage.Blobs.Models;
using BTTWriterCatalog.Models.OutputFormats;
using CsvHelper;
using System.Globalization;

namespace BTTWriterCatalog
{
    public static class Webhook
    {
        [FunctionName("refreshd43chunks")]
        public static async Task<IActionResult> RefreshD43Chunks([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, ILogger log)
        {
            var storageConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnectionString");
            var chunkContainer = Environment.GetEnvironmentVariable("BlobStorageChunkContainer");
            BlobContainerClient outputClient = new BlobContainerClient(storageConnectionString, chunkContainer);
            foreach(var book in Utils.BibleBookOrder)
            {
                log.LogInformation("Uploading chunks for {book}", book);
                var request = new HttpClient();
                var content = await request.GetStringAsync($"https://api.unfoldingword.org/bible/txt/1/{book.ToLower()}/chunks.json");
                var client = outputClient.GetBlobClient(Path.Join("default", book.ToLower(), "chunks.json"));
                await client.UploadAsync( new BinaryData(content), new BlobUploadOptions() { HttpHeaders = new BlobHttpHeaders() { ContentType = "application/json" } } );
            }
            return new OkResult();
        }

        [FunctionName("Clean")]
        public static async Task CleanDeletedResourcesAsync([TimerTrigger("0 0 0 * * *")] TimerInfo timer, ILogger log)
        {
            var databaseConnectionString = Environment.GetEnvironmentVariable("DBConnectionString");
            var databaseName = Environment.GetEnvironmentVariable("DBName");
            var cosmosClient = new CosmosClient(databaseConnectionString);
            Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
            Container deletedResourcesDatabase = await database.CreateContainerIfNotExistsAsync("DeletedResources", "/Partition");
            Container deletedScriptureDatabase = await database.CreateContainerIfNotExistsAsync("DeletedScripture", "/Partition");

            log.LogInformation("Cleaning up Resources database");
            foreach(var item in deletedResourcesDatabase.GetItemLinqQueryable<SupplimentalResourcesModel>().Where(r => r.ModifiedOn < DateTime.Now.AddDays(-2)))
            {
                await deletedResourcesDatabase.DeleteItemAsync<SupplimentalResourcesModel>(item.Id, new PartitionKey(item.Partition));
            }

            foreach(var item in deletedScriptureDatabase.GetItemLinqQueryable<ScriptureResourceModel>().Where(r => r.ModifiedOn < DateTime.Now.AddDays(-2)))
            {
                await deletedScriptureDatabase.DeleteItemAsync<ScriptureResourceModel>(item.DatabaseId, new PartitionKey(item.Partition));
            }
        }

        [FunctionName("webhook")]
        public static async Task<IActionResult> WebhookFunction([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            WebhookEvent webhookEvent = JsonConvert.DeserializeObject<WebhookEvent>(requestBody);

            DateTime timeStarted = DateTime.Now;
            var storageConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnectionString");
            var chunkContainer = Environment.GetEnvironmentVariable("BlobStorageChunkContainer");
            var outputContainer = Environment.GetEnvironmentVariable("BlobStorageOutputContainer");
            var databaseConnectionString = Environment.GetEnvironmentVariable("DBConnectionString");
            var databaseName = Environment.GetEnvironmentVariable("DBName");

            var cosmosClient = new CosmosClient(databaseConnectionString);
            Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
            Container resourcesDatabase = await database.CreateContainerIfNotExistsAsync("Resources", "/Partition");
            Container scriptureDatabase = await database.CreateContainerIfNotExistsAsync("Scripture", "/Partition");
            Container deletedResourcesDatabase = await database.CreateContainerIfNotExistsAsync("DeletedResources", "/Partition");
            Container deletedScriptureDatabase = await database.CreateContainerIfNotExistsAsync("DeletedScripture", "/Partition");



            // validate

            if (webhookEvent == null)
            {
                return new BadRequestObjectResult("Invalid webhook request");
            }

            //Figure out what kind of event this is
            CatalogAction catalogAction = CatalogAction.Unknown;

            if (req.Headers.ContainsKey("X-GitHub-Event"))
            {
                var gitEvent = req.Headers["X-GitHub-Event"];
                if (gitEvent == "push")
                {
                    catalogAction = CatalogAction.Update;
                }
                else if (gitEvent == "repository")
                {
                    if (webhookEvent.action == "create")
                    {
                        catalogAction = CatalogAction.Create;
                    }
                    else if (webhookEvent.action == "delete")
                    {
                        catalogAction = CatalogAction.Delete;
                    }
                }
            }
            #if DEBUG

            // if we're debugging and we aren't specifying an action then just assume that this is an update
            else
            {
                catalogAction = CatalogAction.Update;
            }

            #endif
            if (catalogAction == CatalogAction.Unknown)
            {
                return new OkObjectResult("Unhandled event");
            }
            log.LogInformation("Starting processing for {repository}", webhookEvent.repository.Name);

            log.LogInformation($"Downloading repo");
            var filesDir = Utils.CreateTempFolder();
            var outputDir = Utils.CreateTempFolder();
            using var webClient = new WebClient();
            webClient.DownloadFile($"{webhookEvent.repository.HtmlUrl}/archive/master.zip", Path.Join(filesDir, "repo.zip"));
            var fileSystem = new ZipFileSystem(Path.Join(filesDir, "repo.zip"));
            try
            {
                var basePath = fileSystem.GetFolders().FirstOrDefault();
                var manifestPath = fileSystem.Join(basePath, "manifest.yaml");
                if (!fileSystem.FileExists(manifestPath))
                {
                    throw new Exception("Missing manifest.yaml");
                }

                var reader = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
                var resourceContainer = reader.Deserialize<ResourceContainer>(fileSystem.ReadAllText(manifestPath));
                var repoType = Utils.GetRepoType(resourceContainer?.dublin_core?.identifier);
                var language = resourceContainer?.dublin_core?.language?.identifier;
                if (language == null)
                {
                    throw new Exception("Missing language in manifest");
                }
                if (catalogAction == CatalogAction.Create || catalogAction == CatalogAction.Update)
                {
                    log.LogInformation("Getting chunks for {language}", language);
                    var chunks = await GetResourceChunksAsync(storageConnectionString, chunkContainer, language);

                    // Handle the creation of the content
                    var modifiedTranslationResources = new List<SupplimentalResourcesModel>();
                    var modifiedScriptureResources = new List<ScriptureResourceModel>();
                    string uploadDestination;
                    switch (repoType)
                    {
                        case RepoType.translationNotes:
                            log.LogInformation("Building translationNotes");
                            foreach(var book in TranslationNotes.Convert(fileSystem, basePath, outputDir, resourceContainer, chunks, log))
                            {
                                modifiedTranslationResources.Add(new SupplimentalResourcesModel()
                                {
                                    Book = book,
                                    Language = language,
                                    ResourceType = "tn",
                                    ModifiedOn = DateTime.Now,
                                });
                            }
                            uploadDestination = Path.Join("tn", language);
                            break;
                        case RepoType.translationQuestions:
                            log.LogInformation("Building translationQuestions");
                            foreach(var book in TranslationQuestions.Convert(fileSystem, basePath, outputDir, resourceContainer, log))
                            {
                                modifiedTranslationResources.Add(new SupplimentalResourcesModel()
                                {
                                    Book = book,
                                    Language = language,
                                    ResourceType = "tq",
                                    ModifiedOn = DateTime.Now,
                                });
                            }
                            uploadDestination = Path.Join("tq", language);
                            break;
                        case RepoType.translationWords:
                            log.LogInformation("Building translationWords");
                            TranslationWords.Convert(fileSystem, basePath, outputDir, resourceContainer, log);
                            // Since words are valid for all books then add all of them here
                            foreach(var book in Utils.BibleBookOrder)
                            {
                                modifiedTranslationResources.Add(new SupplimentalResourcesModel()
                                {
                                    Book = book.ToLower(),
                                    Language = language,
                                    ResourceType = "tw",
                                    ModifiedOn = DateTime.Now,
                                });
                            }
                            foreach(var book in TranslationWords.ConvertWordsCatalog(outputDir,await GetTranslationWordCsvForLanguage(storageConnectionString,chunkContainer,language,chunks,log), chunks))
                            {
                                modifiedTranslationResources.Add(new SupplimentalResourcesModel()
                                {
                                    Book = book.ToLower(),
                                    Language = language,
                                    ResourceType = "tw_cat",
                                    ModifiedOn = DateTime.Now,
                                });
                            }
                            uploadDestination = Path.Join("tw", language);
                            break;
                        case RepoType.Bible:
                            log.LogInformation("Building scripture");
                            log.LogInformation("Scanning for chunks");
                            var scriptureChunks = ConversionUtils.GetChunksFromUSFM(GetDocumentsFromZip(fileSystem, log), log);
                            Scripture.Convert(fileSystem, basePath, outputDir, resourceContainer, scriptureChunks);
                            foreach(var project in resourceContainer.projects)
                            {
                                var identifier = project.identifier.ToLower();
                                modifiedScriptureResources.Add(new ScriptureResourceModel()
                                {
                                    Language = language,
                                    LanguageName = resourceContainer.dublin_core.language.title,
                                    LanguageDirection = resourceContainer.dublin_core.language.direction,
                                    Identifier = resourceContainer.dublin_core.identifier,
                                    SourceText = resourceContainer.dublin_core?.source?.FirstOrDefault()?.identifier,
                                    SourceTextVersion = resourceContainer.dublin_core?.source?.FirstOrDefault()?.version,
                                    CheckingEntity = resourceContainer.checking?.checking_entity?.FirstOrDefault(),
                                    CheckingLevel = resourceContainer.checking?.checking_level,
                                    Contributors = resourceContainer.dublin_core.contributor.ToList(),
                                    Version = resourceContainer.dublin_core.version,
                                    ModifiedOn = DateTime.Now,
                                    Title = resourceContainer.dublin_core?.title,
                                    BookName = project.title ?? identifier,
                                    Type = resourceContainer.dublin_core.identifier,
                                    Book = identifier,
                                }) ;
                                // Write out chunk information also
                                File.WriteAllText(Path.Join(outputDir, identifier, "chunks.json"), JsonConvert.SerializeObject(ConversionUtils.ConvertToD43Chunks(chunks[identifier.ToUpper()])));
                            }
                            uploadDestination = Path.Join("bible", language, resourceContainer.dublin_core.identifier);
                            break;
                        default:
                            throw new Exception("Unsupported repo type");
                    }

                    log.LogInformation("Uploading to storage");
                    Utils.UploadToStorage(log, storageConnectionString, outputContainer, outputDir, uploadDestination);

                    if (modifiedTranslationResources.Count > 0)
                    {
                        log.LogInformation("Updating resources in database");
                        await Task.WhenAll(modifiedTranslationResources.Select(i => resourcesDatabase.UpsertItemAsync(i)).ToList());
                    }

                    if (modifiedScriptureResources.Count > 0)
                    {
                        log.LogInformation("Updating scripture in database");
                        await Task.WhenAll(modifiedScriptureResources.Select(i => scriptureDatabase.UpsertItemAsync(i)).ToList());
                    }
                }
                else if (catalogAction == CatalogAction.Delete)
                {
                    log.LogInformation("Starting delete for {repository}", webhookEvent.repository.Name);
                    BlobContainerClient outputClient = new BlobContainerClient(storageConnectionString, outputContainer);
                    string prefix;
                    var resourceTypesToDelete = new List<string>();
                    switch (repoType)
                    {
                        case RepoType.translationNotes:
                            prefix = $"tn/{language}/";
                            resourceTypesToDelete.Add("tn");
                            break;
                        case RepoType.translationQuestions:
                            prefix = $"tq/{language}/";
                            resourceTypesToDelete.Add("tq");
                            break;
                        case RepoType.translationWords:
                            prefix = $"tw/{language}/";
                            resourceTypesToDelete.Add("tw");
                            resourceTypesToDelete.Add("tw_cat");
                            break;
                        case RepoType.Bible:
                            prefix = $"bible/{language}/{resourceContainer.dublin_core.identifier}/";
                            break;
                        default:
                            throw new Exception("Unsupported repo type");
                    }

                    if (prefix != null)
                    {
                        log.LogInformation("Deleting from storage");
                        foreach(var file in await Utils.ListAllFilesUnderPath(outputClient, prefix))
                        {
                            await outputClient.DeleteBlobIfExistsAsync(file);
                        }
                    }

                    log.LogInformation("Deleting from database");
                    if (repoType == RepoType.Bible)
                    {
                        var feed =  scriptureDatabase.GetItemQueryIterator<ScriptureResourceModel>(new QueryDefinition("select * from T where T.Language = @Language and T.Identifier = @Identifier")
                            .WithParameter("@Language", language)
                            .WithParameter("@Identifier", resourceContainer.dublin_core.identifier));
                        while (feed.HasMoreResults)
                        {
                            var items = await feed.ReadNextAsync();
                            foreach(var item in items)
                            {
                                await scriptureDatabase.DeleteItemAsync<ScriptureResourceModel>(item.DatabaseId, new PartitionKey(item.Partition));
                                item.ModifiedOn = DateTime.Now;
                                await deletedScriptureDatabase.UpsertItemAsync(item);
                            }
                        }
                    }
                    else
                    {
                        foreach(var resource in resourceTypesToDelete)
                        {
                            var feed =  resourcesDatabase.GetItemQueryIterator<SupplimentalResourcesModel>(new QueryDefinition("select * from T where T.Language = @Language and T.ResourceType = @ResourceType")
                                .WithParameter("@Language", language)
                                .WithParameter("@ResourceType", resource));
                            while (feed.HasMoreResults)
                            {
                                var items = await feed.ReadNextAsync();
                                foreach(var item in items)
                                {
                                    await resourcesDatabase.DeleteItemAsync<SupplimentalResourcesModel>(item.Id, new PartitionKey(item.Partition));
                                    item.ModifiedOn = DateTime.Now;
                                    await deletedResourcesDatabase.UpsertItemAsync(item);
                                }
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                log.LogError(ex.Message);
                return new BadRequestObjectResult(ex.Message);
            }
            fileSystem.Close();


            Directory.Delete(outputDir, true);
            Directory.Delete(filesDir, true);

            return new OkResult();
        }


        private static List<USFMDocument> GetDocumentsFromZip(ZipFileSystem fileSystem, ILogger log)
        {
            var parser = new USFMParser();
            var output = new List<USFMDocument>();
            foreach (var file in fileSystem.GetAllFiles("usfm"))
            {
                var document = parser.ParseFromString(fileSystem.ReadAllText(file));
                if (document.GetChildMarkers<TOC3Marker>().Count == 0)
                {
                    log.LogWarning("No TOC3 found in document");
                    continue;
                }
                var sections = document.GetChildMarkers<SMarker>();
                if (sections.Count( s=> s.Weight == 5) == 0)
                {
                    log.LogWarning("No chunking information found in source text this will end up as one big chunk");
                }
                output.Add(document);
            }
            return output;
        }
        private static async Task<Dictionary<string,List<WordCatalogCSVRow>>> GetTranslationWordCsvForLanguage(string connectionString, string container, string language, Dictionary<string, Dictionary<int,List<VerseChunk>>> chunks, ILogger log)
        {
            var output = new Dictionary<string,List<WordCatalogCSVRow>>();
            var containerClient = new BlobContainerClient(connectionString, container);
            BlobClient blobClient;
            foreach(var (book,_) in chunks)
            {
                var languageSpecificPath = Path.Join(language, book.ToLower(), "words.csv");
                var defaultPath = Path.Join("default", book.ToLower(), "words.csv");
                blobClient = containerClient.GetBlobClient(languageSpecificPath);
                if (!await blobClient.ExistsAsync())
                {
                    blobClient = containerClient.GetBlobClient(defaultPath);
                    if (!await blobClient.ExistsAsync())
                    {
                        log.LogWarning("No Translation Words CSV exists for {book}", book.ToLower());
                        continue;
                    }
                }
                var stream = await blobClient.OpenReadAsync();
                CsvReader reader = new CsvReader(new StreamReader(stream), CultureInfo.CurrentCulture);
                output.Add(book.ToLower(), reader.GetRecords<WordCatalogCSVRow>().ToList());
            }
            return output;
        }


        private static async Task<Dictionary<string, Dictionary<int,List<VerseChunk>>>> GetResourceChunksAsync(string connectionString, string chunkContainer, string language)
        {
            var output = new Dictionary<string, Dictionary<int,List<VerseChunk>>>();
            var containerClient = new BlobContainerClient(connectionString, chunkContainer);
            BlobClient blobClient;
            foreach(var book in Utils.BibleBookOrder)
            {
                var languageSpecificPath = Path.Join(language, book.ToLower(), "chunks.json");
                var defaultPath = Path.Join("default", book.ToLower(), "chunks.json");
                blobClient = containerClient.GetBlobClient(languageSpecificPath);
                // Check to see if the language specific file exists
                if (!await blobClient.ExistsAsync())
                {
                    // If not fall back
                    blobClient = containerClient.GetBlobClient(defaultPath);
                    if (! await blobClient.ExistsAsync())
                    {
                        // If that is also missing then do nothing
                        output.Add(book, new Dictionary<int,List<VerseChunk>>());
                        continue;
                    }
                }

                var file = new MemoryStream();
                blobClient.DownloadTo(file);
                file.Seek(0, SeekOrigin.Begin);
                var fileContent = new StreamReader(file).ReadToEnd();
                var door43Chunks = JsonConvert.DeserializeObject<List<Door43Chunk>>(fileContent);
                if (door43Chunks != null)
                {
                    output.Add(book, ConversionUtils.ConvertChunks(door43Chunks));
                    continue;
                }
                var waChunks = JsonConvert.DeserializeObject<Dictionary<int,List<VerseChunk>>>(fileContent);
                if (waChunks != null)
                {
                    output.Add(book, waChunks);
                    continue;
                }
                // If we have reached here we don't have chunks in a correct format so just add blank to it
                output.Add(book, new Dictionary<int, List<VerseChunk>>());
            }
            return output;
        }
    }
    enum CatalogAction
    {
        Unknown,
        Create,
        Delete,
        Update
    }
}
