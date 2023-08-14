using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.BIELCatalog
{
    internal class ResourceProject
    {
        [JsonPropertyName("categories")]
        public List<string> Categories {  get; set;}
        [JsonPropertyName("formats")]
        public List<ResourceFormat> Formats {  get; set;}
        [JsonPropertyName("identifier")]
        public string Identifier { get; set; }
        [JsonPropertyName("sort")]
        public int Sort { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; }
        [JsonPropertyName("versification")]
        public string Versification { get; set; }
        public ResourceProject()
        {
            Categories = new List<string>();
            Formats = new List<ResourceFormat>();
        }
    }
}
