using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.BIELCatalog
{
    internal class CatalogRoot
    {
       [JsonPropertyName("languages")]
        public List<BielCatalogLanguage> Languages {  get; set; }
        public CatalogRoot()
        {
            Languages = new List<BielCatalogLanguage>();
        }
    }
}
