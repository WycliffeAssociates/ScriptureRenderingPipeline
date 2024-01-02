using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.BIELCatalog
{
    internal class BielCatalogLanguage
    {
       [JsonPropertyName("direction")]
        public string Direction { get; set; }
       [JsonPropertyName("identifier")]
        public string Identifier { get; set; }
       [JsonPropertyName("resources")]
        public List<BielCatalogResource> Resources { get; set; }
       [JsonPropertyName("title")]
        public string Title { get; set; }
        public BielCatalogLanguage()
        {
            Resources = new List<BielCatalogResource>();
        }
    }
}
