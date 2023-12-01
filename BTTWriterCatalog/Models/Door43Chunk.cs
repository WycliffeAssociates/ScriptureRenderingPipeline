using Newtonsoft.Json;

namespace BTTWriterCatalog.Models
{
    public class Door43Chunk
    {
        [JsonProperty("chp")]
        public string Chapter {  get; set; }
        [JsonProperty("firstvs")]
        public string FirstVerse { get; set; }
    }
}
