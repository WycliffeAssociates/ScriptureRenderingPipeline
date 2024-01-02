using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationWord
    {
       [JsonPropertyName("aliases")]
        public List<string> Aliases { get; set; }
       [JsonPropertyName("cf")]
        public List<string> RelatedWords { get; set; }
       [JsonPropertyName("def")]
        public string Definition { get; set; }
       [JsonPropertyName("def_title")]
        public string DefinitionTitle { get; set; }
       [JsonPropertyName("ex")]
        public List<string> Examples { get; set; }
       [JsonPropertyName("id")]
        public string WordId { get; set; }
        // This is hardcoded in the d43 pipeline, not sure what it does
       [JsonPropertyName("sub")]
        public string Substitute => string.Empty;
       [JsonPropertyName("term")]
        public string Term { get; set; }
        public TranslationWord()
        {
            Aliases = new List<string>();
            RelatedWords = new List<string>();
            Examples = new List<string>();
        }
    }
}
