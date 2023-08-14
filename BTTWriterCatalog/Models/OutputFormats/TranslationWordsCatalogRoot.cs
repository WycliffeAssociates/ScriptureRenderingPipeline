using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationWordsCatalogRoot
    {
        [JsonPropertyName("chapters")]
        public List<TranslationWordsCatalogChapter> Chapters {  get; set; }
        public TranslationWordsCatalogRoot()
        {
            Chapters = new List<TranslationWordsCatalogChapter>();
        }
    }
}
