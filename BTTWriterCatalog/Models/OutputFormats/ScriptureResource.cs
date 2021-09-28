using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class ScriptureModelResource
    {
        [JsonProperty("chapters")]
        public List<ScriptureChapter> Chapters {  get; set; }
        [JsonProperty("date_modified")]
        public string ModifiedOn { get; set; }

        public ScriptureModelResource()
        {
            Chapters = new List<ScriptureChapter>();
        }

    }
}
