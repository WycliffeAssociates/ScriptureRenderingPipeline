using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using BTTWriterCatalog.Models.DataModel;
using BTTWriterCatalog.Models;
using BTTWriterCatalog.ContentConverters;
using BTTWriterCatalog.Helpers;
using USFMToolsSharp.Models.Markers;
using USFMToolsSharp;
using PipelineCommon.Models.Webhook;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;
using Newtonsoft.Json;
using YamlDotNet.Serialization;
using CsvHelper;
using System.Threading;

namespace BTTWriterCatalog
{
    public static class Webhook
    {


        /// <summary>
        /// Refresh chunk definitions from unfoldingWord manually
        /// </summary>
        /// <param name="req">The incoming Http Request that triggered this (unused)</param>
        /// <param name="log">An instance of ILogger</param>
        /// <remarks>We should never need to run this again but I'm keeping it just in case</remarks>
        /// <returns></returns>
        [FunctionName("refreshd43chunks")]
        public static async Task<IActionResult> RefreshD43ChunksAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/refreshd43chunks")] HttpRequest req, ILogger log)
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

        /// <summary>
        /// Timer triggered cleaning function to remove items from the deleted database tables that are older than two days
        /// </summary>
        /// <param name="timer">The triggering timer (unused)</param>
        /// <param name="log">An instance of ILogger</param>
        /// <returns>Nothing</returns>
        [FunctionName("Clean")]
        public static async Task CleanDeletedResourcesAsync([TimerTrigger("0 0 0 * * *")] TimerInfo timer, ILogger log)
        {
            var databaseName = Environment.GetEnvironmentVariable("DBName");

            Database database = await ConversionUtils.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
            Container deletedResourcesDatabase = await database.CreateContainerIfNotExistsAsync("DeletedResources", "/Partition");
            Container deletedScriptureDatabase = await database.CreateContainerIfNotExistsAsync("DeletedScripture", "/Partition");
            var deleteTasks = new List<Task>();

            log.LogInformation("Cleaning up Resources database");
            var resourcesFeed = deletedResourcesDatabase.GetItemQueryIterator<SupplimentalResourcesModel>(new QueryDefinition("select * from T"));
            while (resourcesFeed.HasMoreResults)
            {
                foreach(var item in await resourcesFeed.ReadNextAsync())
                {
                    if (item.ModifiedOn < DateTime.Now.AddDays(-2))
                    {
                        deleteTasks.Add(deletedResourcesDatabase.DeleteItemAsync<SupplimentalResourcesModel>(item.Id, new PartitionKey(item.Partition)));
                    }
                }
            }

            log.LogInformation("Cleaning up Scripture database");
            var scriptureFeed = deletedScriptureDatabase.GetItemQueryIterator<ScriptureResourceModel>(new QueryDefinition("select * from T"));
            while (scriptureFeed.HasMoreResults)
            {
                foreach(var item in await scriptureFeed.ReadNextAsync())
                {
                    if (item.ModifiedOn < DateTime.Now.AddDays(-2))
                    {
                        deleteTasks.Add(deletedScriptureDatabase.DeleteItemAsync<ScriptureResourceModel>(item.DatabaseId, new PartitionKey(item.Partition)));
                    }
                }
            }
            await Task.WhenAll(deleteTasks);
        }

        /// <summary>
        /// Main triggered webhook
        /// </summary>
        /// <param name="req">Incoming webhook request</param>
        /// <param name="log">An instance of ILogger</param>
        /// <returns>Error if any occured otherwise returns nothing but a 204</returns>
        [FunctionName("webhook")]
        public static async Task<IActionResult> WebhookFunctionAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, ILogger log)
        {
            // Convert to a webhook event
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            WebhookEvent webhookEvent = JsonConvert.DeserializeObject<WebhookEvent>(requestBody);

            DateTime timeStarted = DateTime.Now;
            var storageConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnectionString");
            var chunkContainer = Environment.GetEnvironmentVariable("BlobStorageChunkContainer");
            var outputContainer = Environment.GetEnvironmentVariable("BlobStorageOutputContainer");
            var databaseName = Environment.GetEnvironmentVariable("DBName");

            // Get all database connections
            Database database = await ConversionUtils.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
            Container resourcesDatabase = await database.CreateContainerIfNotExistsAsync("Resources", "/Partition");
            Container scriptureDatabase = await database.CreateContainerIfNotExistsAsync("Scripture", "/Partition");
            Container deletedResourcesDatabase = await database.CreateContainerIfNotExistsAsync("DeletedResources", "/Partition");
            Container deletedScriptureDatabase = await database.CreateContainerIfNotExistsAsync("DeletedScripture", "/Partition");
            Container repositoryTypeDatabase = await database.CreateContainerIfNotExistsAsync("RepositoryTypeMapping", "/Partition");

            // Do some request validation

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
                    if (webhookEvent.action == "created")
                    {
                        catalogAction = CatalogAction.Create;
                    }
                    else if (webhookEvent.action == "deleted")
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

            var filesDir = Utils.CreateTempFolder();
            var outputDir = Utils.CreateTempFolder();
            try
            {
                if (catalogAction == CatalogAction.Create || catalogAction == CatalogAction.Update)
                {
                    log.LogInformation($"Downloading repo");

                    using var httpClient = new HttpClient();
                    var httpStream = await httpClient.GetStreamAsync($"{webhookEvent.repository.HtmlUrl}/archive/master.zip");
                    MemoryStream zipStream = new MemoryStream();
                    await httpStream.CopyToAsync(zipStream);
                    var fileSystem = new ZipFileSystem(zipStream);

                    // Load manifest.yaml
                    var basePath = fileSystem.GetFolders().FirstOrDefault();
                    var manifestPath = fileSystem.Join(basePath, "manifest.yaml");
                    if (!fileSystem.FileExists(manifestPath))
                    {
                        throw new Exception("Missing manifest.yaml");
                    }

                    var reader = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
                    ResourceContainer resourceContainer;
                    try
                    {
                         resourceContainer = reader.Deserialize<ResourceContainer>(await fileSystem.ReadAllTextAsync(manifestPath));
                    }
                    catch(Exception ex)
                    {
                        throw new Exception("Problem parsing manifest.yaml", ex);
                    }
                    var repoType = Utils.GetRepoType(resourceContainer?.dublin_core?.identifier);
                    var language = resourceContainer?.dublin_core?.language?.identifier;
                    if (language == null)
                    {
                        throw new Exception("Missing language in manifest");
                    }

                    log.LogInformation("Getting chunks for {language}", language);
                    var chunks = await GetResourceChunksAsync(storageConnectionString, chunkContainer, language);

                    // Process the content
                    var modifiedTranslationResources = new List<SupplimentalResourcesModel>();
                    var modifiedScriptureResources = new List<ScriptureResourceModel>();
                    string uploadDestination;
                    switch (repoType)
                    {
                        case RepoType.translationNotes:
                            log.LogInformation("Building translationNotes");
                            foreach(var book in await TranslationNotes.ConvertAsync(fileSystem, basePath, outputDir, resourceContainer, chunks, log))
                            {
                                modifiedTranslationResources.Add(new SupplimentalResourcesModel()
                                {
                                    Book = book,
                                    Language = language,
                                    ResourceType = "tn",
                                    ModifiedOn = DateTime.Now,
                                    CheckingLevel = resourceContainer?.checking?.checking_level,
                                    CheckingEntities = resourceContainer?.checking?.checking_entity.ToList(),
                                    Title = resourceContainer.dublin_core.title,
                                    BookTitle = resourceContainer?.projects?.FirstOrDefault(p => p.identifier.ToLower() == book.ToLower())?.title ?? null,
                                }) ;
                            }
                            WriteStreamToFile(zipStream, Path.Join(outputDir, "source.zip"));
                            uploadDestination = Path.Join("tn", language);
                            break;
                        case RepoType.translationQuestions:
                            log.LogInformation("Building translationQuestions");
                            foreach(var book in await TranslationQuestions.ConvertAsync(fileSystem, basePath, outputDir, resourceContainer, log))
                            {
                                modifiedTranslationResources.Add(new SupplimentalResourcesModel()
                                {
                                    Book = book,
                                    Language = language,
                                    ResourceType = "tq",
                                    ModifiedOn = DateTime.Now,
                                    CheckingLevel = resourceContainer?.checking?.checking_level,
                                    CheckingEntities = resourceContainer?.checking?.checking_entity.ToList(),
                                    Title = resourceContainer.dublin_core.title,
                                    BookTitle = resourceContainer?.projects?.FirstOrDefault(p => p.identifier.ToLower() == book.ToLower())?.title ?? null,
                                });
                            }
                            WriteStreamToFile(zipStream, Path.Join(outputDir, "source.zip"));
                            uploadDestination = Path.Join("tq", language);
                            break;
                        case RepoType.translationWords:
                            log.LogInformation("Building translationWords");
                            await TranslationWords.ConvertAsync(fileSystem, basePath, outputDir, resourceContainer, log);
                            // Since words are valid for all books then add all of them here
                            foreach(var book in Utils.BibleBookOrder)
                            {
                                modifiedTranslationResources.Add(new SupplimentalResourcesModel()
                                {
                                    Book = book.ToLower(),
                                    Language = language,
                                    ResourceType = "tw",
                                    CheckingLevel = resourceContainer?.checking?.checking_level,
                                    CheckingEntities = resourceContainer?.checking?.checking_entity.ToList(),
                                    ModifiedOn = DateTime.Now,
                                    Title = resourceContainer.dublin_core.title,
                                });
                            }

                            // Since we could be missing information for book potentially then add a seperate tw_cat resource type
                            foreach(var book in await TranslationWords.ConvertWordsCatalogAsync(outputDir,await GetTranslationWordCsvForLanguageAsync(storageConnectionString,chunkContainer,language,chunks,log), chunks))
                            {
                                modifiedTranslationResources.Add(new SupplimentalResourcesModel()
                                {
                                    Book = book.ToLower(),
                                    Language = language,
                                    ResourceType = "tw_cat",
                                    ModifiedOn = DateTime.Now,
                                });
                            }
                            WriteStreamToFile(zipStream, Path.Join(outputDir, "source.zip"));
                            uploadDestination = Path.Join("tw", language);
                            break;
                        case RepoType.Bible:
                            log.LogInformation("Building scripture");
                            log.LogInformation("Scanning for chunks");
                            var scriptureChunks = ConversionUtils.GetChunksFromUSFM(GetDocumentsFromZip(fileSystem, log), log);
                            scriptureChunks = PopulateMissingChunkInformation(scriptureChunks, chunks);
                            log.LogInformation("Building scripture source json");
                            var scriptureOutputTasks = new List<Task>();
                            await Scripture.ConvertAsync(fileSystem, basePath, outputDir, resourceContainer, scriptureChunks, log);
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
                                    PublishedDate = DateTime.Now,
                                    Title = resourceContainer.dublin_core?.title,
                                    BookName = project.title ?? identifier,
                                    Type = resourceContainer.dublin_core.identifier,
                                    Book = identifier,
                                }) ;
                                // Write out chunk information also
                                scriptureOutputTasks.Add(File.WriteAllTextAsync(Path.Join(outputDir, identifier, "chunks.json"), JsonConvert.SerializeObject(ConversionUtils.ConvertToD43Chunks(chunks[identifier.ToUpper()]))));
                            }
                            uploadDestination = Path.Join("bible", language, resourceContainer.dublin_core.identifier);
                            await Task.WhenAll(scriptureOutputTasks);
                            // Output source.zip out so that it will appear in the BIEL catalog
                            WriteStreamToFile(zipStream, Path.Join(outputDir, "source.zip"));
                            break;
                        default:
                            throw new Exception("Unsupported repo type");
                    }

                    log.LogInformation("Uploading to storage");
                    var uploadTasks = new List<Task>
                    {
                        CloudStorageUtils.UploadToStorage(log, storageConnectionString, outputContainer, outputDir, uploadDestination)
                    };

                    if (modifiedTranslationResources.Count > 0)
                    {
                        log.LogInformation("Updating resources in database");
                        foreach(var item in modifiedTranslationResources)
                        {
                            uploadTasks.Add(resourcesDatabase.UpsertItemAsync(item));
                        }
                    }

                    if (modifiedScriptureResources.Count > 0)
                    {
                        log.LogInformation("Updating scripture in database");
                        foreach(var item in modifiedScriptureResources)
                        {
                            uploadTasks.Add(scriptureDatabase.UpsertItemAsync(item));
                        }
                    }

                    // Track what kind of resource this particular repository was so that when it is deleted we know what it was.
                    uploadTasks.Add(repositoryTypeDatabase.UpsertItemAsync(new RepositoryTypeMapping()
                    {
                        Language = language,
                        Type = resourceContainer.dublin_core.identifier,
                        User = webhookEvent.repository.Owner.Username,
                        Repository = webhookEvent.repository.Name
                    }));

                    // Wait for all of the upload and db updates are done
                    await Task.WhenAll(uploadTasks);

                    fileSystem.Close();
                }
                else if (catalogAction == CatalogAction.Delete)
                {
                    log.LogInformation("Starting delete for {repository}", webhookEvent.repository.Name);
                    // Get information about what repo this is from our cache
                    RepositoryTypeMapping repo = new RepositoryTypeMapping();
                    try
                    {
                        repo = (await repositoryTypeDatabase.ReadItemAsync<RepositoryTypeMapping>($"{webhookEvent.repository.Owner.Username}_{webhookEvent.repository.Name}", new PartitionKey("Partition"))).Resource;
                    }
                    catch(CosmosException ex)
                    {
                        if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            throw new Exception("Repo has never been seen by the pipeline so we have no idea what to delete");
                        }
                    }
                    var repoType = Utils.GetRepoType(repo.Type);
                    var language = repo.Language;
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
                            prefix = $"bible/{language}/{repo.Type}/";
                            break;
                        default:
                            throw new Exception("Unsupported repo type");
                    }

                    if (prefix != null)
                    {
                        log.LogInformation("Deleting from storage");
                        foreach(var file in await CloudStorageUtils.ListAllFilesUnderPath(outputClient, prefix))
                        {
                            await outputClient.DeleteBlobIfExistsAsync(file);
                        }
                    }

                    log.LogInformation("Deleting from database");
                    if (repoType == RepoType.Bible)
                    {
                        var feed =  scriptureDatabase.GetItemQueryIterator<ScriptureResourceModel>(new QueryDefinition("select * from T where T.Language = @Language and T.Identifier = @Identifier")
                            .WithParameter("@Language", language)
                            .WithParameter("@Identifier", repo.Type));
                        while (feed.HasMoreResults)
                        {
                            var items = await feed.ReadNextAsync();
                            foreach(var item in items)
                            {
                                await scriptureDatabase.DeleteItemAsync<ScriptureResourceModel>(item.DatabaseId, new PartitionKey(item.Partition));
                                item.ModifiedOn = DateTime.Now;
                                // Since we can't trigger cosmosdb off of a delete then we insert into another database to get that trigger
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
                                // Since we can't trigger cosmosdb off of a delete then we insert into another database to get that trigger
                                    await deletedResourcesDatabase.UpsertItemAsync(item);
                                }
                            }
                        }
                    }

                    //Finally delete this from our list of known repositories
                    await repositoryTypeDatabase.DeleteItemAsync<RepositoryTypeMapping>(repo.Id, new PartitionKey(repo.Partition));
                }
            }
            catch(Exception ex)
            {
                log.LogError(ex.Message);
                return new BadRequestObjectResult(ex.Message);
            }
            finally
            {
                Directory.Delete(outputDir, true);
                Directory.Delete(filesDir, true);
            }

            return new OkResult();
        }

        /// <summary>
        /// Populate missing chunk information in scripture chunks from resource chunks if there is no chunking information for a book
        /// </summary>
        /// <param name="scriptureChunks">An input list of scripture chunks</param>
        /// <param name="resourceChunks">The list of chunks to insert into this if missing</param>
        /// <returns>The combined list of chunks</returns>
        private static Dictionary<string, Dictionary<int, List<VerseChunk>>> PopulateMissingChunkInformation(Dictionary<string, Dictionary<int, List<VerseChunk>>> scriptureChunks, Dictionary<string, Dictionary<int, List<VerseChunk>>> resourceChunks)
        {
            var output = new Dictionary<string, Dictionary<int, List<VerseChunk>>>(resourceChunks.Count);
            foreach(var (book, chapters) in resourceChunks)
            {
                // If there are any chunks for this book then skip it
                if (!scriptureChunks.ContainsKey(book))
                {
                    output.Add(book, chapters);
                    continue;
                }
                if (scriptureChunks[book].Any(i => i.Value.Count != 0))
                {
                    output.Add(book, scriptureChunks[book]);
                    continue;
                }
                // If there is absolutely nothing for this book then add the chunk information from the resources
                output.Add(book, resourceChunks[book]);
            }
            return output;
        }

        /// <summary>
        /// Get a list of USFM documents from a ZipFileSystem
        /// </summary>
        /// <param name="fileSystem">The filesystem</param>
        /// <param name="log">An instance of ILogger</param>
        /// <remarks>This is only used to get books for finding chunks</remarks>
        /// <returns></returns>
        private static List<USFMDocument> GetDocumentsFromZip(ZipFileSystem fileSystem, ILogger log)
        {
            var parser = new USFMParser();
            var output = new List<USFMDocument>();
            foreach (var file in fileSystem.GetAllFiles("usfm"))
            {
                var document = parser.ParseFromString(fileSystem.ReadAllText(file));
                if (document.GetChildMarkers<TOC3Marker>().Count == 0)
                {
                    log.LogWarning("No TOC3 found in {book}", Path.GetFileName(file));
                    continue;
                }
                var sections = document.GetChildMarkers<SMarker>();
                if (sections.Count( s=> s.Weight == 5) == 0)
                {
                    log.LogWarning("No chunking information found in {book} this will end up as one big chunk", Path.GetFileName(file));
                }
                output.Add(document);
            }
            return output;
        }
        /// <summary>
        /// Get the contents of the word mapping CSV in storage 
        /// </summary>
        /// <param name="connectionString">Connection string to the blob storage containing the chunks</param>
        /// <param name="container">The container in azure storage holding these files</param>
        /// <param name="language">The langauge to get the files for</param>
        /// <param name="chunks">A list of chunks to get books from</param>
        /// <param name="log">an instance of ILogger</param>
        /// <returns>A dictionary of book name to list of entries</returns>
        private static async Task<Dictionary<string,List<WordCatalogCSVRow>>> GetTranslationWordCsvForLanguageAsync(string connectionString, string container, string language, Dictionary<string, Dictionary<int,List<VerseChunk>>> chunks, ILogger log)
        {
            var output = new Dictionary<string,List<WordCatalogCSVRow>>();
            var containerClient = new BlobContainerClient(connectionString, container);
            BlobClient blobClient;
            foreach(var (book,_) in chunks)
            {
                var languageSpecificPath = Path.Join(language, book.ToLower(), "words.csv");
                var defaultPath = Path.Join("default", book.ToLower(), "words.csv");
                // Check for language specific first
                blobClient = containerClient.GetBlobClient(languageSpecificPath);
                if (!await blobClient.ExistsAsync())
                {
                    // Check for default next
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


        /// <summary>
        /// Load resource chunk information from storage
        /// </summary>
        /// <param name="connectionString">The storage connection string</param>
        /// <param name="chunkContainer">The container in the storage account to get the chunks from</param>
        /// <param name="language">The language to get chunks for </param>
        /// <returns>Chunk information</returns>
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
                    // If not fall back to default
                    blobClient = containerClient.GetBlobClient(defaultPath);
                    if (! await blobClient.ExistsAsync())
                    {
                        // If that is also missing then do nothing
                        output.Add(book, new Dictionary<int,List<VerseChunk>>());
                        continue;
                    }
                }

                // Skip writing to the FS and keep this in memory
                var file = new MemoryStream();
                await blobClient.DownloadToAsync(file);
                file.Seek(0, SeekOrigin.Begin);
                var fileContent = await new StreamReader(file).ReadToEndAsync();
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
        /// <summary>
        /// Write a stream to a path
        /// </summary>
        /// <param name="input">The stream to write</param>
        /// <param name="path">The filename to write to</param>
        private static void WriteStreamToFile(Stream input, string path)
        {
            var output = File.OpenWrite(path);
            input.Position = 0;
            input.CopyTo(output);
            output.Close();
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
