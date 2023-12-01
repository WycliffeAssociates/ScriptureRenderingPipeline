using Newtonsoft.Json;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class ScriptureFrame
    {
        [JsonProperty("format")]
        public string Format { get; set; }
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("img")]
        public string Image { get; set; }
        [JsonProperty("lastvs")]
        public string LastVerse { get; set; }
        [JsonProperty("text")]
        public string Text { get; set; }
    }
}
