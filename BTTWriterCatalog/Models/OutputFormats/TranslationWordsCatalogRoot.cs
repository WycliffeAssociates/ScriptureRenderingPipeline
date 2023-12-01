using Newtonsoft.Json;
using System.Collections.Generic;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationWordsCatalogRoot
    {
        [JsonProperty("chapters")]
        public List<TranslationWordsCatalogChapter> Chapters {  get; set; }
        public TranslationWordsCatalogRoot()
        {
            Chapters = new List<TranslationWordsCatalogChapter>();
        }
    }
}
