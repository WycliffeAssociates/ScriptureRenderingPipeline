using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationQuestionChapter
    {
        [JsonProperty("id")]
        public string Identifier {  get; set; }

        [JsonProperty("cq")]
        public List<TranslationQuestion> Questions { get; set; }
    }
}
