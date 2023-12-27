using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class ScriptureResource
    {
       [JsonPropertyName("chapters")]
        public List<ScriptureChapter> Chapters {  get; set; }
       [JsonPropertyName("date_modified")]
        public string ModifiedOn { get; set; }

        public ScriptureResource()
        {
            Chapters = new List<ScriptureChapter>();
        }

    }
}
