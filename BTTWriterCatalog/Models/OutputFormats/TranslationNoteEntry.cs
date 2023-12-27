
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationNoteEntry
    {
       [JsonPropertyName("ref")]
        public string Reference { get; set; }
       [JsonPropertyName("text")]
        public string Text { get; set; }
    }
}
