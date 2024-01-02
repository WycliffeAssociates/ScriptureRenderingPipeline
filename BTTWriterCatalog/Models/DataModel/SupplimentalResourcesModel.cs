using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.DataModel
{
    public class SupplimentalResourcesModel
    {
       [JsonPropertyName("id")]
        public string Id => $"{Language}_{ResourceType}_{Book}";
        public string Partition => "Partition";
        public string Language { get; set; }
        public string ResourceType { get; set; }
        public string Book { get; set; }
        public string BookTitle { get; set; }
        public string CheckingLevel { get; set; }
        public List<string> CheckingEntities { get; set; }
        public List<string> Contributors { get; set; }
        public DateTime ModifiedOn { get; set; }
        public string Title { get; set; }
    }
}
