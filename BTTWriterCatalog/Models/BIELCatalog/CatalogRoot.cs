using Newtonsoft.Json;
using System.Collections.Generic;

namespace BTTWriterCatalog.Models.BIELCatalog
{
    internal class CatalogRoot
    {
        [JsonProperty("languages")]
        public List<CatalogLanguage> Languages {  get; set; }
        public CatalogRoot()
        {
            Languages = new List<CatalogLanguage>();
        }
    }
}
