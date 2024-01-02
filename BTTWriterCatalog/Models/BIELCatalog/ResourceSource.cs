
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.BIELCatalog
{
    internal class ResourceSource
    {
       [JsonPropertyName("identifier")]
        public string Identifer { get; set; }
       [JsonPropertyName("langauge")]
        public string Language { get; set; }
       [JsonPropertyName("version")]
        public string Version { get; set; }
    }
}
