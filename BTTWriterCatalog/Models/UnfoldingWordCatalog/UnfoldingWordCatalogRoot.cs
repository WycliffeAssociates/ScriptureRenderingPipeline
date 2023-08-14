using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.UnfoldingWordCatalog
{
    public class UnfoldingWordCatalogRoot
    {
        [JsonPropertyName("cat")]
        public List<UnfoldingWordResource> Catalog { get; set; }
        [JsonPropertyName("mod")]
        public string ModifiedOn { get; set; }
        public UnfoldingWordCatalogRoot()
        {
            Catalog = new List<UnfoldingWordResource>();
        }
    }
    public class UnfoldingWordResource
    {
        [JsonPropertyName("slug")]
        public string Slug { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; }
        [JsonPropertyName("langs")]
        public List<UnfoldingWordLanguage> Languages {  get; set; }

        public UnfoldingWordResource()
        {
            Languages = new List<UnfoldingWordLanguage>();
        }
    }
    public class UnfoldingWordLanguage
    {
        [JsonPropertyName("lc")]
        public string Language {  get; set; }
        [JsonPropertyName("mod")]
        public string ModifiedOn { get; set; }
        [JsonPropertyName("vers")]
        public List<UnfoldingWordVersion> Versions { get; set; }
        public UnfoldingWordLanguage()
        {
            Versions = new List<UnfoldingWordVersion>();
        }
    }
    public class UnfoldingWordVersion
    {
        [JsonPropertyName("mod")]
        public string ModifiedOn { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("slug")]
        public string Slug { get; set; }
        [JsonPropertyName("status")]
        public UnfoldingWordStatus Status { get; set; }
        [JsonPropertyName("toc")]
        public List<UnfoldingWordTableOfContentsEntry> TableOfContents {  get; set; }
        public UnfoldingWordVersion()
        {
            TableOfContents = new List<UnfoldingWordTableOfContentsEntry>();
        }
    }
    public class UnfoldingWordStatus
    {
        [JsonPropertyName("checking_entity")]
        public string CheckingEntity {  get; set; }
        [JsonPropertyName("checking_level")]
        public string CheckingLevel { get; set; }
        [JsonPropertyName("comments")]
        public string Comments { get; set; }
        [JsonPropertyName("contributors")]
        public string Contributors { get; set; }
        [JsonPropertyName("publish_date")]
        public DateTime PublishDate { get; set; }
        [JsonPropertyName("source_text")]
        public string SourceText { get; set; }
        [JsonPropertyName("source_text_version")]
        public string SourceTextVersion { get; set; }
        [JsonPropertyName("version")]
        public string Version { get; set; }
    }
    public class UnfoldingWordTableOfContentsEntry
    {
        [JsonPropertyName("desc")]
        public string Description { get; set; }
        [JsonPropertyName("mod")]
        public string ModifiedOn { get; set; }
        [JsonPropertyName("slug")]
        public string Slug { get; set; }
        [JsonPropertyName("src")]
        public string Source { get; set; }
        [JsonPropertyName("src_sig")]
        public string SourceSignature { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; }
    }
}
