using Newtonsoft.Json;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationNoteEntry
    {
        [JsonProperty("ref")]
        public string Reference { get; set; }
        [JsonProperty("text")]
        public string Text { get; set; }
    }
}
