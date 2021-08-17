using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ScriptureRenderingPipeline.Models.Webhook;
using ScriptureRenderingPipeline.Models.ResourceContainer;
using YamlDotNet.Serialization;
using System.Collections.Generic;
using Azure.Storage.Blobs;
using ScriptureRenderingPipeline.Renderers;
using DotLiquid;
using Azure.Storage.Blobs.Models;
using BTTWriterLib;
using System.Net;
using System.Linq;
using ScriptureRenderingPipeline.Helpers;
using ScriptureRenderingPipeline.Models;

namespace ScriptureRenderingPipeline
{
    public static class Webhook
    {
        private static readonly List<string> BibleIdentifiers = new List<string>()
        {
            "ulb",
            "reg"
        };
        [FunctionName("Webhook")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhook")] HttpRequest req,
            ILogger log)
        {
            var connectionString = Environment.GetEnvironmentVariable("ScripturePipelineStorageConnectionString");
            var outputContainer = Environment.GetEnvironmentVariable("ScripturePipelineStorageOutputContainer");
            var templateContainer = Environment.GetEnvironmentVariable("ScripturePipelineStorageTemplateContainer");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            WebhookEvent webhookEvent = JsonConvert.DeserializeObject<WebhookEvent>(requestBody);

            DateTime timeStarted = DateTime.Now;


            // validate

            if (webhookEvent == null)
            {
                return new BadRequestObjectResult("Invalid webhook request");
            }

            log.LogInformation($"Starting webhook for {webhookEvent.repository.full_name}");

            // download repo

            log.LogInformation($"Downloading repo");
            var filesDir = Utils.CreateTempFolder();
            using var webClient = new WebClient();
            webClient.DownloadFile($"{webhookEvent.repository.html_url}/archive/master.zip", Path.Join(filesDir, "repo.zip"));
            var fileSystem = new ZipFileSystem(Path.Join(filesDir, "repo.zip"));

            RepoType repoType = RepoType.Unknown;
            bool isBTTWriterProject = false;
            var title = "";
            // Determine type of repo
            var basePath = fileSystem.GetFolders().FirstOrDefault();
            if (fileSystem.FileExists(fileSystem.Join(basePath,"manifest.yaml")))
            {
                log.LogInformation("Found manifest.yaml file");
                var reader = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
                var resourceContainer = reader.Deserialize<ResourceContainer>(fileSystem.ReadAllText(fileSystem.Join(basePath,"manifest.yaml")));

                if (resourceContainer == null)
                {
                    log.LogError("Bad manifest file");
                    return new BadRequestObjectResult("Bad manifest file");
                }

                if (resourceContainer?.dublin_core?.identifier != null)
                {
                    title = BuildDisplayName(resourceContainer?.dublin_core?.language?.title, resourceContainer?.dublin_core?.title);
                    repoType = GetRepoType(resourceContainer?.dublin_core?.identifier);
                }
            }
            else if (fileSystem.FileExists(fileSystem.Join(basePath,"manifest.json")))
            {
                isBTTWriterProject = true;
                log.LogInformation("Found BTTWriter project");
                throw new Exception("Can't handle BTTWriter projects until I rewrite the loader");
                var manifest = BTTWriterLoader.GetManifest(new FileSystemResourceContainer(filesDir));
                var languageName = manifest?.target_language?.name;
                var resourceName = manifest?.resource?.name;
                var resourceId = manifest?.resource?.id;


                title = BuildDisplayName(languageName,resourceName);
                repoType = GetRepoType(resourceId);
            }

            if (repoType == RepoType.Unknown)
            {
                return new BadRequestObjectResult("Unable to determine type of repo");
            }

            var outputDir = Utils.CreateTempFolder();
            string converterUsed = "";
            log.LogInformation("Starting render");
            string template;
            switch (repoType)
            {
                case RepoType.Bible:
                    converterUsed = isBTTWriterProject ? "Bible.BTTWriter" : "Bible.Normal";
                    log.LogInformation("Rendering Bible");
                    template = GetTemplate(connectionString, templateContainer, "bible.html");
                    new BibleRenderer().Render(fileSystem, basePath, outputDir, Template.Parse(template), webhookEvent.repository.html_url, title, isBTTWriterProject);
                    break;
                case RepoType.translationNotes:
                    converterUsed = isBTTWriterProject ? "translationNotes.BTTWriter" : "translationNotes.Normal";
                    log.LogInformation("Rendering translationNotes");
                    template = GetTemplate(connectionString, templateContainer, "bible.html");
                    new TranslationNotesRenderer().Render(fileSystem, basePath, outputDir, Template.Parse(template), webhookEvent.repository.html_url, title, isBTTWriterProject);
                    break;
                case RepoType.translationQuestions:
                    converterUsed = isBTTWriterProject ? "translationQuestions.BTTWriter" : "translationQuestions.Normal";
                    log.LogInformation("Rendering translationQuestions");
                    template = GetTemplate(connectionString, templateContainer, "bible.html");
                    new TranslationQuestionsRenderer().Render(fileSystem, basePath, outputDir, Template.Parse(template), webhookEvent.repository.html_url, title, isBTTWriterProject);
                    break;
                default:
                    return new BadRequestObjectResult($"Unable to render type {repoType}");
            }

            // Create the build_log.json
            BuildLog buildLog = new BuildLog()
            {
                success = true,
                ended_at = DateTime.Now,
                created_at = timeStarted,
                started_at = timeStarted,
                commit_message = webhookEvent.commits[0].message,
                committed_by = webhookEvent.commits[0].committer.username,
                commit_url = webhookEvent.commits[0].url,
                commit_id = webhookEvent.commits[0].id,
                convert_module = converterUsed,
                lint_module = null,
                status = "success",
                repo_name = webhookEvent.repository.name,
                repo_owner = webhookEvent.repository.owner.username,
                message = "Conversion successful"
            };

            // Write build log
            File.WriteAllText(Path.Join(outputDir, "build_log.json"), JsonConvert.SerializeObject(buildLog));

            log.LogInformation("Starting upload");
            UploadResult(log, connectionString, outputContainer, outputDir, $"/u/{webhookEvent.repository.owner.username}/{webhookEvent.repository.name}");

            fileSystem.Close();
            log.LogInformation("Cleaning up temporary files");
            Directory.Delete(filesDir, true);
            Directory.Delete(outputDir, true);

            // render template

            return new OkResult();
        }

        private static string BuildDisplayName(string language, string resource)
        {
            return $"{language ?? "Unknown"}: {resource?? "Unknown"}";
        }

        private static void UploadResult(ILogger log, string connectionString, string outputContainer, string outputDir, string basePath)
        {
            BlobContainerClient outputClient = new BlobContainerClient(connectionString, outputContainer);
            outputClient.CreateIfNotExists();
            List<Task> uploadTasks = new List<Task>();
            Parallel.ForEach(Directory.GetFiles(outputDir), (file) =>
            {
                log.LogInformation($"Uploading {Path.GetFileName(file)}");
                var tmp = outputClient.GetBlobClient($"{basePath}/{Path.GetFileName(file)}");
                tmp.DeleteIfExists();
                tmp.Upload(file, new BlobUploadOptions() { HttpHeaders = new BlobHttpHeaders() { ContentType = "text/html" } });
            });
        }

        private static string GetTemplate(string connectionString, string templateContainer, string templateFile)
        {
            BlobClient blobClient = new BlobClient(connectionString, templateContainer, templateFile);
            MemoryStream templateStream = new MemoryStream();
            blobClient.DownloadTo(templateStream);
            templateStream.Seek(0, SeekOrigin.Begin);
            var template = new StreamReader(templateStream).ReadToEnd();
            return template;
        }

        private static RepoType GetRepoType(string resourceIdentifier)
        {
            RepoType repoType = RepoType.Unknown;
            if (BibleIdentifiers.Contains(resourceIdentifier))
            {
                repoType = RepoType.Bible;
            }
            else if (resourceIdentifier == "tn")
            {
                repoType = RepoType.translationNotes;
            }
            else if (resourceIdentifier == "tw")
            {
                repoType = RepoType.translationNotes;
            }
            else if (resourceIdentifier == "tq")
            {
                repoType = RepoType.translationQuestions;
            }
            else if (resourceIdentifier == "ta")
            {
                repoType = RepoType.translationAcademy;
            }

            return repoType;
        }
    }
    enum RepoType
    {
        Unknown,
        Bible,
        bttWriterProject,
        translationWords,
        translationAcademy,
        translationQuestions,
        translationNotes
    }
}
