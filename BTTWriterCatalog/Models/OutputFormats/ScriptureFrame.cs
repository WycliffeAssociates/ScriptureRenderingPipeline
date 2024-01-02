using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class ScriptureFrame
    {
       [JsonPropertyName("format")]
        public string Format { get; set; }
       [JsonPropertyName("id")]
        public string Id { get; set; }
       [JsonPropertyName("img")]
        public string Image { get; set; }
       [JsonPropertyName("lastvs")]
        public string LastVerse { get; set; }
       [JsonPropertyName("text")]
        public string Text { get; set; }
    }
}
