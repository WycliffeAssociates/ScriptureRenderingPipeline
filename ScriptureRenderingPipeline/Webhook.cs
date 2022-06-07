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
using System.Net.Http;
using BTTWriterLib.Models;

namespace ScriptureRenderingPipeline
{
    public static class Webhook
    {
        [FunctionName("Webhook")]
        public static async Task<IActionResult> RunAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhook")] HttpRequest req,
            ILogger log)
        {
            var connectionString = Environment.GetEnvironmentVariable("ScripturePipelineStorageConnectionString");
            var outputContainer = Environment.GetEnvironmentVariable("ScripturePipelineStorageOutputContainer");
            var templateContainer = Environment.GetEnvironmentVariable("ScripturePipelineStorageTemplateContainer");
            var baseUrl = Environment.GetEnvironmentVariable("ScriptureRenderingPipelineBaseUrl");
            var userToRouteResourcesTo = Environment.GetEnvironmentVariable("ScriptureRenderingPipelineResourcesUser");
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

            log.LogInformation("Starting webhook for {repoName}", webhookEvent.repository.FullName);

            // download repo

            log.LogInformation($"Downloading repo");

            using var httpClient = new HttpClient();
            var downloadProjectPageTemplateTask = GetTemplateAsync(connectionString, templateContainer, "project-page.html");
            var downloadPrintPageTemplateTask = GetTemplateAsync(connectionString, templateContainer, "print.html");
            var result = await httpClient.GetAsync($"{webhookEvent.repository.HtmlUrl}/archive/master.zip");
            if(result.StatusCode == HttpStatusCode.NotFound)
            {
                return new BadRequestObjectResult("Can't download source zip, probably an empty repo");
            }
            var zipStream = await result.Content.ReadAsStreamAsync();
            var fileSystem = new ZipFileSystem(zipStream);

            var repoType = RepoType.Unknown;
            var isBTTWriterProject = false;
            var outputDir = Utils.CreateTempFolder();
            string exceptionMessage = null;
            var title = "";
            string template = null;
            var converterUsed = "";
            var languageName = string.Empty;
            var resourceName = string.Empty;
            var languageDirection = "ltr";
            var languageCode = string.Empty;
            try
            {

                // Determine type of repo
                ResourceContainer resourceContainer = null;
                var basePath = fileSystem.GetFolders().FirstOrDefault();
                template = await downloadProjectPageTemplateTask;

                if (fileSystem.FileExists(fileSystem.Join(basePath, "manifest.yaml")))
                {
                    log.LogInformation("Found manifest.yaml file");
                    var reader = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
                    try
                    {
                        resourceContainer = reader.Deserialize<ResourceContainer>(await fileSystem.ReadAllTextAsync(fileSystem.Join(basePath, "manifest.yaml")));
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error loading manifest.yaml {ex.Message}");
                    }

                    if (resourceContainer == null)
                    {
                        throw new Exception("Bad manifest file");
                    }

                    if (resourceContainer?.dublin_core?.identifier != null)
                    {
                        languageName = resourceContainer?.dublin_core?.language?.title;
                        resourceName = resourceContainer?.dublin_core?.title;
                        languageCode = resourceContainer?.dublin_core?.language?.identifier;
                        languageDirection = resourceContainer?.dublin_core?.language?.direction;
                        repoType = Utils.GetRepoType(resourceContainer?.dublin_core?.identifier);
                    }
                }
                else if (fileSystem.FileExists(fileSystem.Join(basePath, "manifest.json")))
                {
                    isBTTWriterProject = true;
                    log.LogInformation("Found BTTWriter project");
                    BTTWriterManifest manifest;
                    try
                    {
                        manifest = BTTWriterLoader.GetManifest(new ZipFileSystemBTTWriterLoader(fileSystem, basePath));
                    }
                    catch(Exception ex)
                    {
                        throw new Exception($"Error loading BTTWriter manifest: {ex.Message}", ex);
                    }
                    languageName = manifest?.target_language?.name;
                    languageCode = manifest?.target_language?.id;
                    resourceName = manifest?.resource?.name;
                    languageDirection = manifest?.target_language?.direction;
                    var resourceId = manifest?.resource?.id;
                    if (string.IsNullOrEmpty(resourceName))
                    {
                        resourceName = resourceId;
                    }


                    repoType = Utils.GetRepoType(resourceId);
                }
                else
                {
                    // Attempt to figure out what this is based on the name of the repo
                    var split = webhookEvent.repository.Name.Split('_');
                    if (split.Length > 1)
                    {
                        var retrieveLanguageTask = TranslationDatabaseInterface.GetLangagueAsync("https://td.unfoldingword.org/exports/langnames.json", split[0]);
                        repoType = Utils.GetRepoType(split[1]);
                        if (repoType == RepoType.Unknown)
                        {
                            if (Utils.BibleBookOrder.Contains(split[1].ToUpper()))
                            {
                                repoType = RepoType.Bible;
                            }
                        }

                        var language = await retrieveLanguageTask;
                        languageName = language?.LanguageName ?? split[0];
                        languageCode = language?.LanguageCode ?? split[0];
                        resourceName = repoType.ToString();
                        languageDirection = language?.Direction;
                    }
                }

                if (repoType == RepoType.Unknown)
                {
                    throw new Exception("Unable to determine type of repo");
                }

                title = BuildDisplayName(languageName, resourceName);

                log.LogInformation("Starting render");
                var printTemplate = await downloadPrintPageTemplateTask;
                switch (repoType)
                {
                    case RepoType.Bible:
                        converterUsed = isBTTWriterProject ? "Bible.BTTWriter" : "Bible.Normal";
                        log.LogInformation("Rendering Bible");
                        await BibleRenderer.RenderAsync(fileSystem, basePath, outputDir, Template.Parse(template), Template.Parse(printTemplate), webhookEvent.repository.HtmlUrl, title, languageDirection, isBTTWriterProject);
                        break;
                    case RepoType.translationNotes:
                        if (resourceContainer == null)
                        {
                            throw new Exception("Can't render translationNotes without a manifest.");
                        }
                        converterUsed = isBTTWriterProject ? "translationNotes.BTTWriter" : "translationNotes.Normal";
                        log.LogInformation("Rendering translationNotes");
                        await new  TranslationNotesRenderer().RenderAsync(fileSystem, basePath, outputDir, Template.Parse(template), Template.Parse(printTemplate), webhookEvent.repository.HtmlUrl, title, baseUrl, userToRouteResourcesTo, languageDirection,  languageCode, isBTTWriterProject);
                        break;
                    case RepoType.translationQuestions:
                        if (resourceContainer == null)
                        {
                            throw new Exception("Can't render translationQuestions without a manifest.");
                        }
                        converterUsed = isBTTWriterProject ? "translationQuestions.BTTWriter" : "translationQuestions.Normal";
                        log.LogInformation("Rendering translationQuestions");
                        await new TranslationQuestionsRenderer().RenderAsync(fileSystem, basePath, outputDir, Template.Parse(template), Template.Parse(printTemplate), webhookEvent.repository.HtmlUrl, title, baseUrl, userToRouteResourcesTo, languageDirection, languageCode, isBTTWriterProject);
                        break;
                    case RepoType.translationWords:
                        if (resourceContainer == null)
                        {
                            throw new Exception("Can't render translationWords without a manifest.");
                        }
                        converterUsed = isBTTWriterProject ? "translationWords.BTTWriter" : "translationWords.Normal";
                        log.LogInformation("Rendering translationWords");
                        await new TranslationWordsRenderer().RenderAsync(fileSystem, basePath, outputDir, Template.Parse(template), Template.Parse(printTemplate), webhookEvent.repository.HtmlUrl, title, resourceContainer, baseUrl, userToRouteResourcesTo, languageDirection, languageCode, isBTTWriterProject);
                        break;
                    case RepoType.translationAcademy:
                        if (resourceContainer == null)
                        {
                            throw new Exception("Can't render translationManual/translationAcademy without a manifest.");
                        }
                        converterUsed = isBTTWriterProject ? "translationManual.BTTWriter" : "translationManual.Normal";
                        log.LogInformation("Rendering translationManual");
                        await new TranslationManualRenderer().RenderAsync(fileSystem, basePath, outputDir, Template.Parse(template), Template.Parse(printTemplate), webhookEvent.repository.HtmlUrl, title, resourceContainer, baseUrl, userToRouteResourcesTo, languageDirection, languageCode, isBTTWriterProject);
                        break;
                    case RepoType.BibleCommentary:
                        converterUsed = "bibleCommentary.Normal";
                        log.LogInformation("Rendering Bible Commentary");
                        await new CommentaryRenderer().RenderAsync(fileSystem, basePath, outputDir, Template.Parse(template), Template.Parse(printTemplate), webhookEvent.repository.HtmlUrl, title, resourceContainer, baseUrl, userToRouteResourcesTo, languageDirection, isBTTWriterProject);
                        break;
                    default:
                        throw new Exception($"Unable to render type {repoType}");
                }
            }
            catch (Exception e)
            {
                log.LogError(e, e.Message);
                exceptionMessage = e.Message;
            }

            // Create the build_log.json
            BuildLog buildLog = new BuildLog()
            {
                success = string.IsNullOrEmpty(exceptionMessage),
                ended_at = DateTime.Now,
                created_at = timeStarted,
                started_at = timeStarted,
                convert_module = converterUsed,
                lint_module = null,
                status = string.IsNullOrEmpty(exceptionMessage) ? "success" : "failure",
                repo_name = webhookEvent.repository.Name,
                repo_owner = webhookEvent.repository.Owner.Username,
                message = string.IsNullOrEmpty(exceptionMessage) ? "Conversion successful" : "Conversion failed",
                warnings = Array.Empty<string>(),
            };
            if (webhookEvent.commits.Length != 0)
            {
                buildLog.commit_message = webhookEvent.commits[0].Message;
                buildLog.committed_by = webhookEvent.commits[0].Committer.Username;
                buildLog.commit_url = webhookEvent.commits[0].Url;
                buildLog.commit_id = webhookEvent.commits[0].Id;
            }
            else
            {
                log.LogWarning("There were no commits in the push so not the information in the build_log.json won't have this");
            }

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
            await Utils.UploadToStorage(log, connectionString, outputContainer, outputDir, $"/u/{webhookEvent.repository.Owner.Username}/{webhookEvent.repository.Name}");

            fileSystem.Close();
            log.LogInformation("Cleaning up temporary files");

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


        private static async Task<string> GetTemplateAsync(string connectionString, string templateContainer, string templateFile)
        {
            BlobClient blobClient = new BlobClient(connectionString, templateContainer, templateFile);
            MemoryStream templateStream = new MemoryStream();
            await blobClient.DownloadToAsync(templateStream);
            templateStream.Seek(0, SeekOrigin.Begin);
            return await new StreamReader(templateStream).ReadToEndAsync();
        }

    }
}
