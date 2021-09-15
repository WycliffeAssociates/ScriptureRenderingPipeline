using Newtonsoft.Json;
using System.Collections.Generic;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationQuestion
    {
        [JsonProperty("q")]
        public string Question { get; set; }
        [JsonProperty("a")]
        public string Answer { get; set; }
        [JsonProperty("ref")]
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
