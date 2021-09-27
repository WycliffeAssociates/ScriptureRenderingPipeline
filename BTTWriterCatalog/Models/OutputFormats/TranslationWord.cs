using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationWord
    {
        [JsonProperty("aliases")]
        public List<string> Aliases { get; set; }
        [JsonProperty("cf")]
        public List<string> RelatedWords { get; set; }
        [JsonProperty("def")]
        public string Definition { get; set; }
        [JsonProperty("def_title")]
        public string DefinitionTitle { get; set; }
        [JsonProperty("ex")]
        public List<string> Examples { get; set; }
        [JsonProperty("id")]
        public string WordId { get; set; }
        // This is hardcoded in the d43 pipeline, not sure what it does
        [JsonProperty("sub")]
        public string Substitute => string.Empty;
        public string Term { get; set; }
        public TranslationWord()
        {
            Aliases = new List<string>();
            RelatedWords = new List<string>();
            Examples = new List<string>();
        }
    }
}
