using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class ScriptureChapter
    {
        [JsonProperty("frames")]
        public List<ScriptureFrame> Frames { get; set; }
        [JsonProperty("number")]
        public string ChapterNumber { get; set; }
        [JsonProperty("ref")]
        public string Reference { get; set; }
        [JsonProperty("title")]
        public string Title {  get; set; }

        public ScriptureChapter()
        {
            Frames = new List<ScriptureFrame>();
        }
    }
}
