using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationWordCatalogItem
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public TranslationWordCatalogItem(string id)
        {
            Id = id;
        }
    }
}
