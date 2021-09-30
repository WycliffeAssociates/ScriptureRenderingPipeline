using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BTTWriterCatalog.Models.DataModel
{
    internal class RepositoryTypeMapping
    {
        [JsonProperty("id")]
        public string Id => $"{User}_{Repository}";
        public string Partition => "Partition";
        public string User { get; set; }
        public string Repository { get; set; }
        public string Type { get; set; }
        public string Language { get; set; }
    }
}
