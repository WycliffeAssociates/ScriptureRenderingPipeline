using System;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.BIELCatalog
{
    internal class ResourceFormat
    {
       [JsonPropertyName("format")]
        public string Format { get; set; }
       [JsonPropertyName("modified")]
        public DateTime ModifidOn { get; set; }
       [JsonPropertyName("signature")]
        public string SignatureUrl { get; set; }
       [JsonPropertyName("size")]
        public int Size { get; set; }
       [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}
