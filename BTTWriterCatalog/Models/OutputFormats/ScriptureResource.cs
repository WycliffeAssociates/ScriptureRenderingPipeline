using Newtonsoft.Json;
using System.Collections.Generic;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class ScriptureResource
    {
        [JsonProperty("chapters")]
        public List<ScriptureChapter> Chapters {  get; set; }
        [JsonProperty("date_modified")]
        public string ModifiedOn { get; set; }

        public ScriptureResource()
        {
            Chapters = new List<ScriptureChapter>();
        }

    }
}
