using Newtonsoft.Json;
using System;

namespace BTTWriterCatalog.Models.BIELCatalog
{
    internal class ResourceFormat
    {
        [JsonProperty("format")]
        public string Format { get; set; }
        [JsonProperty("modified")]
        public DateTime ModifidOn { get; set; }
        [JsonProperty("signature")]
        public string SignatureUrl { get; set; }
        [JsonProperty("size")]
        public int Size { get; set; }
        [JsonProperty("url")]
        public string Url { get; set; }
    }
}
