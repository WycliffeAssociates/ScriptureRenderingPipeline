using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationNoteChunk
    {
       [JsonPropertyName("id")]
        public string Id { get; set; }
       [JsonPropertyName("tn")]
        public List<TranslationNoteEntry> Notes { get; set; }

        public TranslationNoteChunk()
        {
            Notes = new List<TranslationNoteEntry>();
        }
    }
}
