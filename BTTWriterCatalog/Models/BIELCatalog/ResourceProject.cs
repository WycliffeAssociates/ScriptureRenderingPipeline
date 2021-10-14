using Newtonsoft.Json;
using System.Collections.Generic;

namespace BTTWriterCatalog.Models.BIELCatalog
{
    internal class ResourceProject
    {
        [JsonProperty("categories")]
        public List<string> Categories {  get; set;}
        [JsonProperty("formats")]
        public List<ResourceFormat> Formats {  get; set;}
        [JsonProperty("identifier")]
        public string Identifier { get; set; }
        [JsonProperty("sort")]
        public int Sort { get; set; }
        [JsonProperty("title")]
        public string Title { get; set; }
        [JsonProperty("versification")]
        public string Versification { get; set; }
        public ResourceProject()
        {
            Categories = new List<string>();
            Formats = new List<ResourceFormat>();
        }
    }
}
