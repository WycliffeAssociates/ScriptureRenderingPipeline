using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

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
