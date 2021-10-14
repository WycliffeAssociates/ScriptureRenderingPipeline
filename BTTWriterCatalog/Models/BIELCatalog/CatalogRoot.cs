using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

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
