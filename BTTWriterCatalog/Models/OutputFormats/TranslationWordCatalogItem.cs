
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationWordCatalogItem
    {
       [JsonPropertyName("id")]
        public string Id { get; set; }
        public TranslationWordCatalogItem(string id)
        {
            Id = id;
        }
    }
}
