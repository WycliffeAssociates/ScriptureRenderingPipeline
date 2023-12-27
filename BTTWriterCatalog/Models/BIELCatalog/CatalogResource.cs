using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.BIELCatalog
{
    internal class CatalogResource
    {
       [JsonPropertyName("checking")]
        public ResourceCheckingInformation Checking { get; set; }
       [JsonPropertyName("comment")]
        public string Comment { get; set; }
       [JsonPropertyName("contributor")]
        public List<string> Contributors { get; set; }
       [JsonPropertyName("creator")]
        public string Creator { get; set; }
       [JsonPropertyName("description")]
        public string Description {  get; set; }
       [JsonPropertyName("formats")]
        public List<ResourceFormat> Formats { get; set; }
       [JsonPropertyName("identifier")]
        public string Identifier { get; set; }
       [JsonPropertyName("issued")]
        public DateTime Issued { get; set; }
       [JsonPropertyName("modified")]
        public DateTime ModifiedOn { get; set; }
       [JsonPropertyName("projects")]
        public List<ResourceProject> Projects { get; set; }
       [JsonPropertyName("publisher")]
        public string Publisher { get; set; }
       [JsonPropertyName("relation")]
        public List<string> Relation { get; set; }
       [JsonPropertyName("rights")]
        public string Rights { get; set; }
       [JsonPropertyName("source")]
        public List<ResourceSource> Sources { get; set; }
       [JsonPropertyName("subject")]
        public string Subject { get; set; }
       [JsonPropertyName("title")]
        public string Title {  get; set; }
       [JsonPropertyName("version")]
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
