using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models
{
    public class Door43Chunk
    {
        [JsonPropertyName("chp")]
        public string Chapter {  get; set; }
        [JsonPropertyName("firstvs")]
        public string FirstVerse { get; set; }
    }
}
