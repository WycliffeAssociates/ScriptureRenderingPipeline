using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PipelineCommon.Models.Webhook;
using PipelineCommon.Models.ResourceContainer;
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
using PipelineCommon.Helpers;

namespace ScriptureRenderingPipeline
{
    public static class Webhook
    {
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

            if (req.Headers.ContainsKey("X-GitHub-Event"))
            {
                if (req.Headers["X-GitHub-Event"] != "push")
                {
                    return new OkObjectResult("Not converting because this isn't a push event");
                }
            }

            if (webhookEvent.commits == null)
            {
                return new BadRequestObjectResult("Missing commits from webhook event");
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
            var outputDir = Utils.CreateTempFolder();
            string exceptionMessage = null;
            var title = "";
            string template = null;
            string converterUsed = "";
            // Determine type of repo
            try
            {

                ResourceContainer resourceContainer = null;
                var basePath = fileSystem.GetFolders().FirstOrDefault();
                template = GetTemplate(connectionString, templateContainer, "project-page.html");

                if (fileSystem.FileExists(fileSystem.Join(basePath, "manifest.yaml")))
                {
                    log.LogInformation("Found manifest.yaml file");
                    var reader = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
                    try
                    {
                        resourceContainer = reader.Deserialize<ResourceContainer>(fileSystem.ReadAllText(fileSystem.Join(basePath, "manifest.yaml")));
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex.Message);
                    }

                    if (resourceContainer == null)
                    {
                        throw new Exception("Bad manifest file");
                    }

                    if (resourceContainer?.dublin_core?.identifier != null)
                    {
                        title = BuildDisplayName(resourceContainer?.dublin_core?.language?.title, resourceContainer?.dublin_core?.title);
                        repoType = Utils.GetRepoType(resourceContainer?.dublin_core?.identifier);
                    }
                }
                else if (fileSystem.FileExists(fileSystem.Join(basePath, "manifest.json")))
                {
                    isBTTWriterProject = true;
                    log.LogInformation("Found BTTWriter project");
                    var manifest = BTTWriterLoader.GetManifest(new ZipFileSystemBTTWriterLoader(fileSystem, basePath));
                    var languageName = manifest?.target_language?.name;
                    var resourceName = manifest?.resource?.name;
                    var resourceId = manifest?.resource?.id;
                    if (string.IsNullOrEmpty(resourceName))
                    {
                        resourceName = resourceId;
                    }


                    title = BuildDisplayName(languageName, resourceName);
                    repoType = Utils.GetRepoType(resourceId);
                }

                if (repoType == RepoType.Unknown)
                {
                    throw new Exception("Unable to determine type of repo");
                }

                log.LogInformation("Starting render");
                var printTemplate = GetTemplate(connectionString, templateContainer, "print.html");
                switch (repoType)
                {
                    case RepoType.Bible:
                        converterUsed = isBTTWriterProject ? "Bible.BTTWriter" : "Bible.Normal";
                        log.LogInformation("Rendering Bible");
                        new BibleRenderer().Render(fileSystem, basePath, outputDir, Template.Parse(template), Template.Parse(printTemplate), webhookEvent.repository.html_url, title, isBTTWriterProject);
                        break;
                    case RepoType.translationNotes:
                        converterUsed = isBTTWriterProject ? "translationNotes.BTTWriter" : "translationNotes.Normal";
                        log.LogInformation("Rendering translationNotes");
                        new TranslationNotesRenderer().Render(fileSystem, basePath, outputDir, Template.Parse(template), Template.Parse(printTemplate), webhookEvent.repository.html_url, title, isBTTWriterProject);
                        break;
                    case RepoType.translationQuestions:
                        converterUsed = isBTTWriterProject ? "translationQuestions.BTTWriter" : "translationQuestions.Normal";
                        log.LogInformation("Rendering translationQuestions");
                        new TranslationQuestionsRenderer().Render(fileSystem, basePath, outputDir, Template.Parse(template), Template.Parse(printTemplate), webhookEvent.repository.html_url, title, isBTTWriterProject);
                        break;
                    case RepoType.translationWords:
                        converterUsed = isBTTWriterProject ? "translationWords.BTTWriter" : "translationWords.Normal";
                        log.LogInformation("Rendering translationWords");
                        new TranslationWordsRenderer().Render(fileSystem, basePath, outputDir, Template.Parse(template), Template.Parse(printTemplate), webhookEvent.repository.html_url, title, resourceContainer, isBTTWriterProject);
                        break;
                    case RepoType.translationAcademy:
                        converterUsed = isBTTWriterProject ? "translationManual.BTTWriter" : "translationManual.Normal";
                        log.LogInformation("Rendering translationManual");
                        new TranslationManualRenderer().Render(fileSystem, basePath, outputDir, Template.Parse(template), Template.Parse(printTemplate), webhookEvent.repository.html_url, title, resourceContainer, isBTTWriterProject);
                        break;
                    default:
                        throw new Exception($"Unable to render type {repoType}");
                }
            }
            catch (Exception e)
            {
                log.LogError(e.Message);
                exceptionMessage = e.Message;
            }

            // Create the build_log.json
            BuildLog buildLog = new BuildLog()
            {
                success = string.IsNullOrEmpty(exceptionMessage),
                ended_at = DateTime.Now,
                created_at = timeStarted,
                started_at = timeStarted,
                commit_message = webhookEvent.commits[0].message,
                committed_by = webhookEvent.commits[0].committer.username,
                commit_url = webhookEvent.commits[0].url,
                commit_id = webhookEvent.commits[0].id,
                convert_module = converterUsed,
                lint_module = null,
                status = string.IsNullOrEmpty(exceptionMessage) ? "success" : "failure",
                repo_name = webhookEvent.repository.name,
                repo_owner = webhookEvent.repository.owner.username,
                message = string.IsNullOrEmpty(exceptionMessage) ? "Conversion successful" : "Conversion failed"
            };

            // Write build log
            File.WriteAllText(Path.Join(outputDir, "build_log.json"), JsonConvert.SerializeObject(buildLog));
            if (!string.IsNullOrEmpty(exceptionMessage))
            {
                string errorPage = "";
                if (string.IsNullOrEmpty(template))
                {
                    errorPage = "<h1>Render Error</h1> Unable to load template so falling back to plain html <br/>" + exceptionMessage;
                }
                else
                {
                    errorPage = Template.Parse(template).Render(Hash.FromAnonymousObject( new { content="<h1>Render Error</h1> " + exceptionMessage }));
                }
                File.WriteAllText(Path.Join(outputDir, "index.html"), errorPage);
            }

            log.LogInformation("Starting upload");
            Utils.UploadToStorage(log, connectionString, outputContainer, outputDir, $"/u/{webhookEvent.repository.owner.username}/{webhookEvent.repository.name}");

            fileSystem.Close();
            log.LogInformation("Cleaning up temporary files");
            if (Directory.Exists(filesDir))
            {
                Directory.Delete(filesDir, true);
            }
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, true);
            }
            if (!string.IsNullOrEmpty(exceptionMessage))
            {
                return new BadRequestObjectResult(exceptionMessage);
            }
            return new OkResult();
        }

        private static string BuildDisplayName(string language, string resource)
        {
            return $"{language ?? "Unknown"}: {resource ?? "Unknown"}";
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

    }
}
