using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationWordCatalogFrame
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("items")]
        public List<TranslationWordCatalogItem> Items { get; set; }
        public TranslationWordCatalogFrame(string id)
        {
            Id = id;
            Items = new List<TranslationWordCatalogItem>();
        }
        public TranslationWordCatalogFrame()
        {
            Items = new List<TranslationWordCatalogItem>();
        }
    }
}
