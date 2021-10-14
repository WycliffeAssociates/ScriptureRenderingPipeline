using Newtonsoft.Json;

namespace BTTWriterCatalog.Models.BIELCatalog
{
    internal class ResourceSource
    {
        [JsonProperty("identifier")]
        public string Identifer { get; set; }
        [JsonProperty("langauge")]
        public string Language { get; set; }
        [JsonProperty("version")]
        public string Version { get; set; }
    }
}
