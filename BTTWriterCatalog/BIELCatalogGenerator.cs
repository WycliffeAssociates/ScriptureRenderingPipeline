using BTTWriterCatalog.Helpers;
using BTTWriterCatalog.Models.BIELCatalog;
using BTTWriterCatalog.Models.DataModel;
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
    public class BIELCatalogGenerator
    {
        [FunctionName("BIELCatalogManualBuild")]
        public static async Task<IActionResult> ManualBuild([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route ="api/BIELCatalogManualBuild")] HttpRequest req, ILogger log)
        {
            await BuildCatalogAsync(log);
            return new OkResult();
        }
        [FunctionName("BIELCatalogAutomaticBuild")]
        public static async Task TriggerFromDBAsync([CosmosDBTrigger(
            databaseName: "BTTWriterCatalog",
            collectionName: "Scripture",
            ConnectionStringSetting = "DBConnectionString",
            CreateLeaseCollectionIfNotExists = true,
            LeaseCollectionPrefix = "BIELCatalog",
            LeaseCollectionName = "leases")]IReadOnlyList<Microsoft.Azure.Documents.Document> input, ILogger log)
        {
            await BuildCatalogAsync(log);
        }

        [FunctionName("BIELCatalogAutomaticBuildFromDelete")]
        public static async Task TriggerFromDBDeleteAsync([CosmosDBTrigger(
            databaseName: "BTTWriterCatalog",
            collectionName: "DeletedScripture",
            ConnectionStringSetting = "DBConnectionString",
            CreateLeaseCollectionIfNotExists = true,
            LeaseCollectionPrefix = "BIELCatalog",
            LeaseCollectionName = "leases")]IReadOnlyList<Microsoft.Azure.Documents.Document> input, ILogger log)
        {
            await BuildCatalogAsync(log);
        }
        private static async Task BuildCatalogAsync(ILogger log)
        {
            var databaseName = Environment.GetEnvironmentVariable("DBName");
            var storageConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnectionString");
            var storageCatalogContainer = Environment.GetEnvironmentVariable("BlobStorageOutputContainer");
            var catalogBaseUrl = Environment.GetEnvironmentVariable("CatalogBaseUrl");
            var outputDir = Utils.CreateTempFolder();

            Database database = ConversionUtils.cosmosClient.GetDatabase(databaseName);
            Container scriptureDatabase = database.GetContainer("Scripture");
            Container resourcesDatabase = database.GetContainer("Resources");
            log.LogInformation("Getting all scripture resources");
            var scriptureResourceTask = GetAllScriptureResources(scriptureDatabase);
            var supplimentalResourceTask = GetAllSupplimentalResources(resourcesDatabase);
            var output = new CatalogRoot();
            AddScriptureToCatalog(catalogBaseUrl, await scriptureResourceTask, output);
            AddResourcesToCatalog(catalogBaseUrl, await supplimentalResourceTask, output);

            // Order projects
            foreach(var langauge in output.Languages)
            {
                foreach(var resource in langauge.Resources)
                {
                    resource.Projects = resource.Projects.OrderBy(i => i.Sort).ToList();
                }
            }

            Directory.CreateDirectory(Path.Join(outputDir, "v3"));
            await File.WriteAllTextAsync(Path.Join(outputDir, "/v3/catalog.json"), JsonConvert.SerializeObject(output));
            log.LogInformation("Uploading to storage");
            await Utils.UploadToStorage(log, storageConnectionString, storageCatalogContainer, outputDir, "/");
        }


        /// <summary>
        /// Add all scripture resources to the catalog
        /// </summary>
        /// <param name="catalogBaseUrl">The base URL of the catalog for the links</param>
        /// <param name="allScriptureResources">A list of all of the scripture resources to insert</param>
        /// <param name="output">The catalog to write out to</param>
        private static void AddScriptureToCatalog(string catalogBaseUrl, List<ScriptureResourceModel> allScriptureResources, CatalogRoot output)
        {
            foreach (var item in allScriptureResources)
            {
                // If we don't have an entry for this language already then insert one
                var language = output.Languages.FirstOrDefault(l => l.Identifier == item.Language);
                if (language == null)
                {
                    language = new CatalogLanguage()
                    {
                        Identifier = item.Language,
                        Direction = item.LanguageDirection,
                        Title = item.LanguageName,
                    };
                    output.Languages.Add(language);
                }
                // If we don't have an entry for this resource than enter it
                var resource = language.Resources.FirstOrDefault(i => i.Identifier == item.Identifier);
                if (resource == null)
                {
                    resource = new CatalogResource()
                    {
                        Checking = new ResourceCheckingInformation()
                        {
                            CheckingEntities = new List<string>() { item.CheckingEntity },
                            CheckingLevel = item.CheckingLevel
                        },
                        Comment = item.Comments,
                        Contributors = item.Contributors,
                        Creator = string.Empty,
                        Description = String.Empty,
                        Subject = "Bible",
                        Formats = new List<ResourceFormat>()
                        {
                            new ResourceFormat()
                            {
                                Format = "application/zip; type=bundle content=text/usfm conformsto=rc0.2",
                                ModifidOn = item.ModifiedOn,
                                SignatureUrl = string.Empty,
                                Size = 0,
                                Url = $"{catalogBaseUrl}/bible/{item.Language.ToLower()}/{item.Identifier.ToLower()}/source.zip"
                            }
                        },
                        Identifier = item.Identifier,
                        Issued = item.CreatedOn,
                        ModifiedOn = item.ModifiedOn,
                        Publisher = "Wycliffe Associates",
                        Title = item.Title,
                        Version = item.Version,
                    };
                    language.Resources.Add(resource);
                }

                // And finally add the book to this resource
                var bookNumber = Utils.GetBookNumber(item.Book);
                resource.Projects.Add(new ResourceProject()
                {
                    Identifier = item.Book,
                    Sort = bookNumber,
                    Title = item.BookName,
                    Categories = new List<string>() { bookNumber < 40 ? "bible-ot" : "bible-nt" },
                    Formats = new List<ResourceFormat>()
                    {
                        new ResourceFormat()
                        {
                            Format = "text/usfm",
                            ModifidOn = item.ModifiedOn,
                            SignatureUrl = "",
                            Size = 0,
                            Url = $"{catalogBaseUrl}/bible/{item.Language.ToLower()}/{item.Identifier.ToLower()}/{item.Book.ToLower()}/source.usfm",
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Add supplimental resources to the catalog
        /// </summary>
        /// <param name="catalogBaseUrl">The base URL of the catalog</param>
        /// <param name="allResources">A list of all resources</param>
        /// <param name="output">The catalog to add resources to</param>
        private static void AddResourcesToCatalog(string catalogBaseUrl, List<SupplimentalResourcesModel> allResources, CatalogRoot output)
        {
            foreach(var item in allResources)
            {
                var language = output.Languages.FirstOrDefault(l => l.Identifier == item.Language);
                if (language == null)
                {
                    language = new CatalogLanguage()
                    {
                        Identifier = item.Language,
                        Title = item.Language,
                    };
                    output.Languages.Add(language);
                }
                var resource = language.Resources.FirstOrDefault(i => i.Identifier == item.ResourceType);
                if (resource == null)
                {
                    resource = new CatalogResource()
                    {
                        Creator = string.Empty,
                        Description = string.Empty,
                        Identifier = item.ResourceType,
                        Issued = item.ModifiedOn,
                        Subject = GetSubjectForResource(item.ResourceType),
                        Checking = new ResourceCheckingInformation()
                        {
                            CheckingLevel = item.CheckingLevel,
                            CheckingEntities = item.CheckingEntities,
                        },
                        ModifiedOn = item.ModifiedOn,
                        Title = item.Title,
                        Publisher = "Wycliffe Associates",
                    };
                    // For some reason tw formats are in project not in the top level
                    if (item.ResourceType != "tw")
                    {
                        resource.Formats = new List<ResourceFormat>()
                        {
                            new ResourceFormat()
                            {
                                Format = "application/zip; type=bundle content=text/md conformsto = rc0.2",
                                ModifidOn = item.ModifiedOn,
                                SignatureUrl = string.Empty,
                                Size = 0,
                                Url = $"{catalogBaseUrl}/{item.ResourceType.ToLower()}/{item.Language.ToLower()}/source.zip"
                            }
                        };
                    }
                    language.Resources.Add(resource);
                }

                // If this is tn or tq (which have books) add the projects
                if (item.ResourceType == "tn" || item.ResourceType == "tq")
                {
                    resource.Projects.Add(new ResourceProject()
                    {
                        Identifier = item.Book,
                        Sort = Utils.GetBookNumber(item.Book),
                        Versification = string.Empty,
                        Title = item.BookTitle,
                    });
                }
                // For translationWords only add one item
                else if (item.ResourceType == "tw" && resource.Projects.Count == 0)
                {
                    resource.Projects.Add(new ResourceProject()
                    {
                        Identifier = "bible",
                        Sort = 0,
                        Title = item.Title,
                        Formats = new List<ResourceFormat>()
                        {
                            new ResourceFormat()
                            {
                                Format = "application/zip; type=dict content = text/markdown conformsto=rc0.2",
                                ModifidOn = item.ModifiedOn,
                                Url = $"{catalogBaseUrl}/tn/{item.Language.ToLower()}/source.zip"
                            }
                        }
                    });
                }
            }
        }
        /// <summary>
        /// Get subject for a resource. Essentially just a lookup
        /// </summary>
        /// <param name="input">The identifier for a resource</param>
        /// <returns>The display name for the resource</returns>
        private static string GetSubjectForResource(string input)
        {
            switch (input)
            {
                case "tn":
                    return "Translation Notes";
                case "tq":
                    return "Translation Questions";
                case "tw":
                    return "Translation Words";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Get a list of scripture resources
        /// </summary>
        /// <param name="database">The database container to get the resources from</param>
        /// <returns>The list of supplemental resources</returns>
        private static async Task<List<ScriptureResourceModel>> GetAllScriptureResources(Container database)
        {
            var output = new List<ScriptureResourceModel>();
            var feed = database.GetItemQueryIterator<ScriptureResourceModel>("select * from T");
            while (feed.HasMoreResults)
            {
                output.AddRange(await feed.ReadNextAsync());
            }
            return output;
        }
        /// <summary>
        /// Get all supplimental resources. Except for tw_cats
        /// </summary>
        /// <param name="database">The database container to get the list from</param>
        /// <returns>A list of supplimental resources</returns>
        private static async Task<List<SupplimentalResourcesModel>> GetAllSupplimentalResources(Container database)
        {
            var output = new List<SupplimentalResourcesModel>();
            var feed = database.GetItemQueryIterator<SupplimentalResourcesModel>("select * from T where T.ResourceType != 'tw_cat'");
            while (feed.HasMoreResults)
            {
                output.AddRange(await feed.ReadNextAsync());
            }
            return output;
        }
    }
}
