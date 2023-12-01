using Newtonsoft.Json;

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
