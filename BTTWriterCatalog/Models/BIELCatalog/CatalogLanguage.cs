using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.BIELCatalog
{
    internal class CatalogLanguage
    {
       [JsonPropertyName("direction")]
        public string Direction { get; set; }
       [JsonPropertyName("identifier")]
        public string Identifier { get; set; }
       [JsonPropertyName("resources")]
        public List<CatalogResource> Resources { get; set; }
       [JsonPropertyName("title")]
        public string Title { get; set; }
        public CatalogLanguage()
        {
            Resources = new List<CatalogResource>();
        }
    }
}
