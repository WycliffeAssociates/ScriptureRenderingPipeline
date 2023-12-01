using Newtonsoft.Json;
using System.Collections.Generic;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationNoteChunk
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("tn")]
        public List<TranslationNoteEntry> Notes { get; set; }

        public TranslationNoteChunk()
        {
            Notes = new List<TranslationNoteEntry>();
        }
    }
}
