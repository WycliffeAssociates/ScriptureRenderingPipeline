using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.BIELCatalog
{
    internal class CatalogRoot
    {
        [JsonPropertyName("languages")]
        public List<CatalogLanguage> Languages {  get; set; }
        public CatalogRoot()
        {
            Languages = new List<CatalogLanguage>();
        }
    }
}
