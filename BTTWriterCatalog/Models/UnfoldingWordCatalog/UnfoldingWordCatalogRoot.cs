using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BTTWriterCatalog.Models.UnfoldingWordCatalog
{
    public class UnfoldingWordCatalogRoot
    {
        [JsonProperty("cat")]
        public List<UnfoldingWordResource> Catalog { get; set; }
        [JsonProperty("mod")]
        public string ModifiedOn { get; set; }
        public UnfoldingWordCatalogRoot()
        {
            Catalog = new List<UnfoldingWordResource>();
        }
    }
    public class UnfoldingWordResource
    {
        [JsonProperty("slug")]
        public string Slug { get; set; }
        [JsonProperty("title")]
        public string Title { get; set; }
        [JsonProperty("langs")]
        public List<UnfoldingWordLanguage> Languages {  get; set; }
    }
    public class UnfoldingWordLanguage
    {
        [JsonProperty("lc")]
        public string Language {  get; set; }
        [JsonProperty("mod")]
        public string ModifiedOn { get; set; }
        [JsonProperty("vers")]
        public List<UnfoldingWordVersion> Versions { get; set; }
    }
    public class UnfoldingWordVersion
    {
        [JsonProperty("mod")]
        public string ModifiedOn { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("slug")]
        public string Slug { get; set; }
        [JsonProperty("status")]
        public List<UnfoldingWordStatus> Status { get; set; }
        [JsonProperty("toc")]
        public List<UnfoldingWordTableOfContentsEntry> TableOfContents {  get; set; }
    }
    public class UnfoldingWordStatus
    {
        [JsonProperty("checking_entity")]
        public string CheckingEntity {  get; set; }
        [JsonProperty("checking_level")]
        public string CheckingLevel { get; set; }
        [JsonProperty("comments")]
        public string Comments { get; set; }
        [JsonProperty("contributors")]
        public string Contributors { get; set; }
        [JsonProperty("publish_date")]
        public DateTime PublishDate { get; set; }
        [JsonProperty("source_text")]
        public string SourceText { get; set; }
        [JsonProperty("source_text_version")]
        public string SourceTextVersion { get; set; }
        [JsonProperty("version")]
        public string Version { get; set; }
    }
    public class UnfoldingWordTableOfContentsEntry
    {
        [JsonProperty("desc")]
        public string Description { get; set; }
        [JsonProperty("mod")]
        public string ModifiedOn { get; set; }
        [JsonProperty("slug")]
        public string Slug { get; set; }
        [JsonProperty("src")]
        public string Source { get; set; }
        [JsonProperty("src_sig")]
        public string SourceSignature { get; set; }
        [JsonProperty("title")]
        public string Title { get; set; }
    }
}
