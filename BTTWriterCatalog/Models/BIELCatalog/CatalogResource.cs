using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace BTTWriterCatalog.Models.BIELCatalog
{
    internal class CatalogResource
    {
        [JsonProperty("checking")]
        public ResourceCheckingInformation Checking { get; set; }
        [JsonProperty("comment")]
        public string Comment { get; set; }
        [JsonProperty("contributor")]
        public List<string> Contributors { get; set; }
        [JsonProperty("creator")]
        public string Creator { get; set; }
        [JsonProperty("description")]
        public string Description {  get; set; }
        [JsonProperty("formats")]
        public List<ResourceFormat> Formats { get; set; }
        [JsonProperty("identifier")]
        public string Identifier { get; set; }
        [JsonProperty("issued")]
        public DateTime Issued { get; set; }
        [JsonProperty("modified")]
        public DateTime ModifiedOn { get; set; }
        [JsonProperty("projects")]
        public List<ResourceProject> Projects { get; set; }
        [JsonProperty("publisher")]
        public string Publisher { get; set; }
        [JsonProperty("relation")]
        public List<string> Relation { get; set; }
        [JsonProperty("rights")]
        public string Rights { get; set; }
        [JsonProperty("source")]
        public List<ResourceSource> Sources { get; set; }
        [JsonProperty("subject")]
        public string Subject { get; set; }
        [JsonProperty("title")]
        public string Title {  get; set; }
        [JsonProperty("version")]
        public string Version { get; set; }
        public CatalogResource()
        {
            Contributors = new List<string>();
            Formats = new List<ResourceFormat>();
            Projects = new List<ResourceProject>();
            Sources = new List<ResourceSource>();
        }
    }
}
