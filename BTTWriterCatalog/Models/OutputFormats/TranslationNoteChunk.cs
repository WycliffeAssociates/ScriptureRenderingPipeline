using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
