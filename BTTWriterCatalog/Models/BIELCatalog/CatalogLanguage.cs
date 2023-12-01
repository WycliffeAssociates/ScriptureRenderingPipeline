using Newtonsoft.Json;
using System.Collections.Generic;

namespace BTTWriterCatalog.Models.BIELCatalog
{
    internal class CatalogLanguage
    {
        [JsonProperty("direction")]
        public string Direction { get; set; }
        [JsonProperty("identifier")]
        public string Identifier { get; set; }
        [JsonProperty("resources")]
        public List<CatalogResource> Resources { get; set; }
        [JsonProperty("title")]
        public string Title { get; set; }
        public CatalogLanguage()
        {
            Resources = new List<CatalogResource>();
        }
    }
}
