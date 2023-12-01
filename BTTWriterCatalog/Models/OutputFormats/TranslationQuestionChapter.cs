using Newtonsoft.Json;
using System.Collections.Generic;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationQuestionChapter
    {
        [JsonProperty("id")]
        public string Identifier {  get; set; }

        [JsonProperty("cq")]
        public List<TranslationQuestion> Questions { get; set; }

        public TranslationQuestionChapter()
        {
            Questions = new List<TranslationQuestion>();
        }
    }
}
