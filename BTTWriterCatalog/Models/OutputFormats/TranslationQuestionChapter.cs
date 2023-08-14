using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationQuestionChapter
    {
        [JsonPropertyName("id")]
        public string Identifier {  get; set; }

        [JsonPropertyName("cq")]
        public List<TranslationQuestion> Questions { get; set; }

        public TranslationQuestionChapter()
        {
            Questions = new List<TranslationQuestion>();
        }
    }
}
