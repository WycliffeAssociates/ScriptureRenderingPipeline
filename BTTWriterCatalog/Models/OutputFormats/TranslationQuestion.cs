using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationQuestion
    {
       [JsonPropertyName("q")]
        public string Question { get; set; }
       [JsonPropertyName("a")]
        public string Answer { get; set; }
       [JsonPropertyName("ref")]
        public List<string> References { get; set; }
        public TranslationQuestion()
        {
            References = new List<string>();
        }
        public TranslationQuestion(string question, string answer)
        {
            Question = question;
            Answer = answer;
            References = new List<string>();
        }
    }
}
