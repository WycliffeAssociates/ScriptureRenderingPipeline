using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BTTWriterCatalog.Models.DataModel
{
    public class SupplimentalResources
    {
        [JsonProperty("id")]
        public string Id => $"{Language}_{ResourceType}_{Book}";
        public string Partition => "Partition";
        public string Language { get; set; }
        public string ResourceType { get; set; }
        public string Book { get; set; }
        public DateTime ModifiedOn { get; set; }
    }
}
