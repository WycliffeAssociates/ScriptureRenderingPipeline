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

namespace BTTWriterCatalog
{
    public static class Webhook
    {
        [FunctionName("LanguageTest")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var languageCode = req.Query["code"];
            var languageName = req.Query["name"];
            // local connection to emulator
            var connectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
            var cosmosClient = new CosmosClient(connectionString);
            var databaseName = "BTTCatalog";
            var containerName = "Languages";
            Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
            Container container = await database.CreateContainerIfNotExistsAsync(containerName,"/Partition");
            /*Language test = new Language()
            {
                Direction = "ltr",
                Name = languageName,
                Slug = languageCode,
            };
            await container.UpsertItemAsync(test);
            */
            return new OkResult();
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

            log.LogInformation($"Downloading repo");
            var filesDir = Utils.CreateTempFolder();
            var outputDir = Utils.CreateTempFolder();
            using var webClient = new WebClient();
            webClient.DownloadFile($"{webhookEvent.repository.html_url}/archive/master.zip", Path.Join(filesDir, "repo.zip"));
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
                log.LogInformation("Getting chunks for {language}", language);
                var chunks = await GetResourceChunksAsync(storageConnectionString, chunkContainer, language);
                if (catalogAction == CatalogAction.Create || catalogAction == CatalogAction.Update)
                {

                    // Handle the creation of the content
                    string uploadDestination;
                    switch (repoType)
                    {
                        case RepoType.translationNotes:
                            log.LogInformation("Building translationNotes");
                            TranslationNotes.Convert(fileSystem, basePath, outputDir, resourceContainer, chunks);
                            uploadDestination = Path.Join("tn", language);
                            break;
                        case RepoType.translationQuestions:
                            log.LogInformation("Building translationQuestions");
                            TranslationQuestions.Convert(fileSystem, basePath, outputDir, resourceContainer);
                            uploadDestination = Path.Join("tq", language);
                            break;
                        case RepoType.Bible:
                            log.LogInformation("Building scripture");
                            log.LogInformation("Scanning for chunks");
                            chunks = ConversionUtils.GetChunksFromUSFM(GetDocumentsFromZip(fileSystem, log));
                            Scripture.Convert(fileSystem, basePath, outputDir, resourceContainer, chunks);
                            uploadDestination = Path.Join("bible", language);
                            break;
                        default:
                            throw new Exception("Unsupported repo type");
                    }

                    // TODO: Handle inserting into DB

                    log.LogInformation("Uploading to storage");
                    Utils.UploadToStorage(log, storageConnectionString, outputContainer, outputDir, uploadDestination);
                }
                else if (catalogAction == CatalogAction.Delete)
                {
                    // TODO: Handle delete
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
        private static void RemoveFromCatalogDB()
        {
            throw new NotImplementedException();
        }
        private static void EnsureExistanceInCatalogDB()
        {
            throw new NotImplementedException();
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
