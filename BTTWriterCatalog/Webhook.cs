using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc;
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
using YamlDotNet.Serialization;
using CsvHelper;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;

namespace BTTWriterCatalog
{
    public class Webhook
    {
        private ILogger<Webhook> log;
        public Webhook(ILogger<Webhook> logger)
        {
            log = logger;
        }
        /// <summary>
        /// Refresh chunk definitions from unfoldingWord manually
        /// </summary>
        /// <param name="req">The incoming Http Request that triggered this (unused)</param>
        /// <remarks>We should never need to run this again but I'm keeping it just in case</remarks>
        /// <returns></returns>
        [Function("refreshd43chunks")]
        public  async Task<IActionResult> RefreshD43ChunksAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/refreshd43chunks")] HttpRequest req)
        {
            log.LogInformation("Starting to refresh D43 chunks");
            var chunkContainer = Environment.GetEnvironmentVariable("BlobStorageChunkContainer");
            var blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("BlobStorageConnectionString"));
            var outputClient = blobServiceClient.GetBlobContainerClient(chunkContainer);
            if (!await outputClient.ExistsAsync())
            {
                await outputClient.CreateAsync();
            }
            foreach(var book in Utils.BibleBookOrder)
            {
                log.LogInformation("Uploading chunks for {Book}", book);
                var content = await Utils.httpClient.GetStringAsync($"https://api.unfoldingword.org/bible/txt/1/{book.ToLower()}/chunks.json");
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
        [Function("Clean")]
        public async Task CleanDeletedResourcesAsync([TimerTrigger("0 0 0 * * *")] TimerInfo timer)
        {
            var databaseName = Environment.GetEnvironmentVariable("DBName");

            var database = (await ConversionUtils.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName)).Database;
            var deletedResourcesDatabase = await database.CreateContainerIfNotExistsAsync("DeletedResources", "/Partition");
            var deletedScriptureDatabase = await database.CreateContainerIfNotExistsAsync("DeletedScripture", "/Partition");
            var deleteTasks = new List<Task>();

            log.LogInformation("Cleaning up Resources database");
            var resourcesFeed = deletedResourcesDatabase.Container.GetItemQueryIterator<SupplementalResourcesModel>(new QueryDefinition("select * from T"));
            while (resourcesFeed.HasMoreResults)
            {
                foreach(var item in await resourcesFeed.ReadNextAsync())
                {
                    if (item.ModifiedOn < DateTime.Now.AddDays(-2))
                    {
                        deleteTasks.Add(deletedResourcesDatabase.Container.DeleteItemAsync<SupplementalResourcesModel>(item.id, new PartitionKey(item.Partition)));
                    }
                }
            }

            log.LogInformation("Cleaning up Scripture database");
            var scriptureFeed = deletedScriptureDatabase.Container.GetItemQueryIterator<ScriptureResourceModel>(new QueryDefinition("select * from T"));
            while (scriptureFeed.HasMoreResults)
            {
                foreach(var item in await scriptureFeed.ReadNextAsync())
                {
                    if (item.ModifiedOn < DateTime.Now.AddDays(-2))
                    {
                        deleteTasks.Add(deletedScriptureDatabase.Container.DeleteItemAsync<ScriptureResourceModel>(item.id, new PartitionKey(item.Partition)));
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
        [Function("webhook")]
        public async Task<IActionResult> WebhookFunctionAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
        {
            // Convert to a webhook event
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var webhookEvent = JsonSerializer.Deserialize<WebhookEvent>(requestBody);

            var blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("BlobStorageConnectionString"));
            var chunkContainer = Environment.GetEnvironmentVariable("BlobStorageChunkContainer");
            var outputContainer = Environment.GetEnvironmentVariable("BlobStorageOutputContainer");
            var databaseName = Environment.GetEnvironmentVariable("DBName");
			var allowedDomain = Environment.GetEnvironmentVariable("AllowedDomain");
            

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
            
			if (!string.IsNullOrEmpty(allowedDomain))
			{
				try
				{
					var url = new Uri(webhookEvent.repository.HtmlUrl);
					if (url.Host != allowedDomain)
					{
						log.LogError("Webhooks for {Domain} are not allowed", url.Host);
						return new BadRequestObjectResult("Webhooks for this domain are not allowed");
					}
				}
				catch (Exception ex)
				{
					log.LogError(ex, "Error validating domain");
					return new BadRequestObjectResult("Invalid url");
				}
			}

            //Figure out what kind of event this is
            var catalogAction = CatalogAction.Unknown;

            if (req.Headers.ContainsKey("X-GitHub-Event"))
            {
                var gitEvent = req.Headers["X-GitHub-Event"];
                //var gitEvent = req.Headers.GetValues("X-GitHub-Event").First();
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

            // if we're debugging, and we aren't specifying an action then just assume that this is an update
            else
            {
                catalogAction = CatalogAction.Update;
            }

            #endif
            if (catalogAction == CatalogAction.Unknown)
            {
                return new OkObjectResult("Unhandled event");
            }

            log.LogInformation("Starting processing for {Repository}", webhookEvent.repository.Name);

            try
            {
                if (catalogAction == CatalogAction.Create || catalogAction == CatalogAction.Update)
                {
                    await HandleUpsert(webhookEvent, chunkContainer, outputContainer, resourcesDatabase, scriptureDatabase, repositoryTypeDatabase, log);
                }
                else if (catalogAction == CatalogAction.Delete)
                {
                    await HandleDelete(webhookEvent, repositoryTypeDatabase, outputContainer, scriptureDatabase, deletedScriptureDatabase, resourcesDatabase, deletedResourcesDatabase, log);
                }
            }
            catch(Exception ex)
            {
                log.LogError(ex.Message);
                return new BadRequestObjectResult(ex.Message);
            }

            return new OkResult();
        }

        private static async Task HandleDelete(WebhookEvent webhookEvent, Container repositoryTypeDatabase, string outputContainer,
            Container scriptureDatabase, Container deletedScriptureDatabase, Container resourcesDatabase,
            Container deletedResourcesDatabase, ILogger log)
        {
            log.LogInformation("Starting delete for {Repository}", webhookEvent.repository.Name);
            var blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("BlobStorageConnectionString"));
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
            var outputClient = blobServiceClient.GetBlobContainerClient(outputContainer);
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

            log.LogInformation("Deleting from storage");
            foreach(var file in await CloudStorageUtils.ListAllFilesUnderPath(outputClient, prefix))
            {
                await outputClient.DeleteBlobIfExistsAsync(file);
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
                        await scriptureDatabase.DeleteItemAsync<ScriptureResourceModel>(item.id, new PartitionKey(item.Partition));
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
                    var feed =  resourcesDatabase.GetItemQueryIterator<SupplementalResourcesModel>(new QueryDefinition("select * from T where T.Language = @Language and T.ResourceType = @ResourceType")
                        .WithParameter("@Language", language)
                        .WithParameter("@ResourceType", resource));
                    while (feed.HasMoreResults)
                    {
                        var items = await feed.ReadNextAsync();
                        foreach(var item in items)
                        {
                            await resourcesDatabase.DeleteItemAsync<SupplementalResourcesModel>(item.id, new PartitionKey(item.Partition));
                            item.ModifiedOn = DateTime.Now;
                            // Since we can't trigger cosmosdb off of a delete then we insert into another database to get that trigger
                            await deletedResourcesDatabase.UpsertItemAsync(item);
                        }
                    }
                }
            }

            //Finally delete this from our list of known repositories
            await repositoryTypeDatabase.DeleteItemAsync<RepositoryTypeMapping>(repo.id, new PartitionKey(repo.Partition));
        }

        private static async Task HandleUpsert(WebhookEvent webhookEvent, string chunkContainer, string outputContainer,
            Container resourcesDatabase, Container scriptureDatabase, Container repositoryTypeDatabase, ILogger log)
        {
            DirectAzureUpload outputInterface;
            log.LogInformation($"Downloading repo");
            var blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("BlobStorageConnectionString"));

            var response = await Utils.httpClient.GetAsync(
                Utils.GenerateDownloadLink(webhookEvent.repository.HtmlUrl, webhookEvent.repository.Owner.Username,
                    webhookEvent.repository.Name, webhookEvent.repository.default_branch ?? "master"));
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Error downloading repo got response code: {response.StatusCode}");
            }
            var httpStream = await response.Content.ReadAsStreamAsync();
            var zipStream = new MemoryStream();
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

            log.LogInformation("Getting chunks for {Language}", language);
            var chunks = await GetResourceChunksAsync(blobServiceClient, chunkContainer, language);

            // Process the content
            var modifiedTranslationResources = new List<SupplementalResourcesModel>();
            var modifiedScriptureResources = new List<ScriptureResourceModel>();
            switch (repoType)
            {
                case RepoType.translationNotes:
                    outputInterface = new DirectAzureUpload(Path.Join("tn", language), blobServiceClient.GetBlobContainerClient(outputContainer));
                    log.LogInformation("Building translationNotes");
                    foreach(var book in await TranslationNotes.ConvertAsync(fileSystem, basePath, outputInterface, resourceContainer, chunks, log))
                    {
                        modifiedTranslationResources.Add(new SupplementalResourcesModel()
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
                    await WriteSourceZipAsync(zipStream, outputInterface);
                    break;
                case RepoType.translationQuestions:
                    log.LogInformation("Building translationQuestions");
                    outputInterface = new DirectAzureUpload(Path.Join("tq", language), blobServiceClient.GetBlobContainerClient(outputContainer));
                    foreach(var book in await TranslationQuestions.ConvertAsync(fileSystem, basePath, outputInterface, resourceContainer, log))
                    {
                        modifiedTranslationResources.Add(new SupplementalResourcesModel()
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
                    await WriteSourceZipAsync(zipStream, outputInterface);
                    break;
                case RepoType.translationWords:
                    log.LogInformation("Building translationWords");
                    outputInterface = new DirectAzureUpload(Path.Join("tw", language), blobServiceClient.GetBlobContainerClient(outputContainer));
                    await TranslationWords.ConvertAsync(fileSystem, basePath, outputInterface, resourceContainer, log);
                    // Since words are valid for all books then add all of them here
                    foreach(var book in Utils.BibleBookOrder)
                    {
                        modifiedTranslationResources.Add(new SupplementalResourcesModel()
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

                    // Since we could be missing information for book potentially then add a separate tw_cat resource type
                    foreach(var book in await TranslationWords.ConvertWordsCatalogAsync(outputInterface, await GetTranslationWordCsvForLanguageAsync(blobServiceClient,chunkContainer,language,chunks,log), chunks))
                    {
                        modifiedTranslationResources.Add(new SupplementalResourcesModel()
                        {
                            Book = book.ToLower(),
                            Language = language,
                            ResourceType = "tw_cat",
                            ModifiedOn = DateTime.Now,
                        });
                    }
                    await WriteSourceZipAsync(zipStream, outputInterface);
                    break;
                case RepoType.Bible:
                    log.LogInformation("Building scripture");
                    log.LogInformation("Scanning for chunks");
                    outputInterface = new DirectAzureUpload(Path.Join("bible", language, resourceContainer.dublin_core.identifier), blobServiceClient.GetBlobContainerClient(outputContainer));
                    var scriptureChunks = ConversionUtils.GetChunksFromUSFM(GetDocumentsFromZip(fileSystem, log), log);
                    scriptureChunks = PopulateMissingChunkInformation(scriptureChunks, chunks);
                    log.LogInformation("Building scripture source json");
                    var scriptureOutputTasks = new List<Task>();
                    var convertedBooks = await Scripture.ConvertAsync(fileSystem, basePath, outputInterface, resourceContainer, scriptureChunks, log);
                    foreach(var project in resourceContainer.projects)
                    {
                        if (!convertedBooks.Contains(project.identifier.ToLower()))
                        {
                            log.LogWarning("Book {Book} was not converted, skipping", project.identifier);
                            continue;
                        }
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
                        scriptureOutputTasks.Add(outputInterface.WriteAllTextAsync(Path.Join(identifier, "chunks.json"), JsonSerializer.Serialize(ConversionUtils.ConvertToD43Chunks(chunks[identifier.ToUpper()]))));
                    }
                    await Task.WhenAll(scriptureOutputTasks);
                            
                    await WriteSourceZipAsync(zipStream, outputInterface);
                    break;
                default:
                    throw new Exception("Unsupported repo type");
            }

            log.LogInformation("Uploading to storage");
            var uploadTasks = new List<Task> { outputInterface.FinishAsync() };

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

        /// <summary>
        /// Write out the source zip to the output interface
        /// </summary>
        /// <param name="zipStream">The source stream</param>
        /// <param name="outputInterface">The interface to write out to</param>
        private static async Task WriteSourceZipAsync(Stream zipStream, IOutputInterface outputInterface)
        {
            zipStream.Position = 0;
            await outputInterface.WriteStreamAsync("source.zip", zipStream);
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
            var parser = new USFMParser(ignoreUnknownMarkers:true);
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
        /// <param name="serviceClient">Blob Service Client for the storage</param>
        /// <param name="container">The container in azure storage holding these files</param>
        /// <param name="language">The langauge to get the files for</param>
        /// <param name="chunks">A list of chunks to get books from</param>
        /// <param name="log">an instance of ILogger</param>
        /// <returns>A dictionary of book name to list of entries</returns>
        private static async Task<Dictionary<string,List<WordCatalogCSVRow>>> GetTranslationWordCsvForLanguageAsync(BlobServiceClient serviceClient, string container, string language, Dictionary<string, Dictionary<int,List<VerseChunk>>> chunks, ILogger log)
        {
            var output = new Dictionary<string,List<WordCatalogCSVRow>>();
            var containerClient = serviceClient.GetBlobContainerClient(container);
            foreach(var (book,_) in chunks)
            {
                var languageSpecificPath = Path.Join(language, book.ToLower(), "words.csv");
                var defaultPath = Path.Join("default", book.ToLower(), "words.csv");
                // Check for language specific first
                var blobClient = containerClient.GetBlobClient(languageSpecificPath);
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
                var reader = new CsvReader(new StreamReader(stream), CultureInfo.CurrentCulture);
                output.Add(book.ToLower(), reader.GetRecords<WordCatalogCSVRow>().ToList());
            }
            return output;
        }


        /// <summary>
        /// Load resource chunk information from storage
        /// </summary>
        /// <param name="serviceClient">A service client for </param>
        /// <param name="chunkContainer">The container in the storage account to get the chunks from</param>
        /// <param name="language">The language to get chunks for </param>
        /// <returns>Chunk information</returns>
        private static async Task<Dictionary<string, Dictionary<int,List<VerseChunk>>>> GetResourceChunksAsync(BlobServiceClient serviceClient, string chunkContainer, string language)
        {
            var output = new Dictionary<string, Dictionary<int,List<VerseChunk>>>();
            var containerClient = serviceClient.GetBlobContainerClient(chunkContainer);
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
                var door43Chunks = JsonSerializer.Deserialize(fileContent, CatalogJsonContext.Default.ListDoor43Chunk);
                if (door43Chunks != null)
                {
                    output.Add(book, ConversionUtils.ConvertChunks(door43Chunks));
                    continue;
                }
                var waChunks = JsonSerializer.Deserialize<Dictionary<int,List<VerseChunk>>>(fileContent);
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
