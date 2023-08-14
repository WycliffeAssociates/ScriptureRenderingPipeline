using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class ScriptureChapter
    {
        [JsonPropertyName("frames")]
        public List<ScriptureFrame> Frames { get; set; }
        [JsonPropertyName("number")]
        public string ChapterNumber { get; set; }
        [JsonPropertyName("ref")]
        public string Reference { get; set; }
        [JsonPropertyName("title")]
        public string Title {  get; set; }

        public ScriptureChapter()
        {
            Frames = new List<ScriptureFrame>();
        }
    }
}
