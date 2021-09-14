using System;
using System.Collections.Generic;
using BTTWriterCatalog.Models.DataModel;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BTTWriterCatalog
{
    public static class DatabaseTriggerTestFunction
    {
        [FunctionName("DatabaseTriggerTestFunction")]
        public static void Run([CosmosDBTrigger(
            databaseName: "BTTCatalog",
            collectionName: "Languages",
            ConnectionStringSetting = "DBConnectionString",
            CreateLeaseCollectionIfNotExists = true,
            LeaseCollectionName = "leases")]IReadOnlyList<Document> input, ILogger log)
        {
            var languages = input.Select(i => JsonConvert.DeserializeObject<Language>(i.ToString()));
            foreach(var language in languages)
            {
                log.LogInformation(language.Slug);
                log.LogInformation(language.Name);
                log.LogInformation(language.Direction);
            }
        }
    }
}
