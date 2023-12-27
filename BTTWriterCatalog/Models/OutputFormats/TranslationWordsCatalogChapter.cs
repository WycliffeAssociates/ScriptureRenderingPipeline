using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationWordsCatalogChapter
    {
       [JsonPropertyName("frames")]
        public List<TranslationWordCatalogFrame> Frames { get; set; }
       [JsonPropertyName("id")]
        public string Id { get; set; }
        public TranslationWordsCatalogChapter(string id)
        {
            Id = id;
            Frames = new List<TranslationWordCatalogFrame>();
        }
        public TranslationWordsCatalogChapter()
        {
            Frames = new List<TranslationWordCatalogFrame>();
        }
    }
}
