using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationWordCatalogFrame
    {
       [JsonPropertyName("id")]
        public string Id { get; set; }
       [JsonPropertyName("items")]
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
