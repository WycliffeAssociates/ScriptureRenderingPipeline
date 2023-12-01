using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace BTTWriterCatalog.Models.DataModel
{
    public class ScriptureResourceModel
    {
        [JsonProperty("id")]
        public string DatabaseId => $"{Language}_{Identifier}_{Book}";
        public string Identifier { get; set; }
        public string Partition => "Partition";
        public string Language { get; set; }
        public DateTime ModifiedOn { get; set; }
        public DateTime CreatedOn {  get; set; }
        public string CheckingEntity { get; set; }
        public string CheckingLevel { get; set; }
        public string Comments { get; set; }
        public List<string> Contributors { get; set; }
        public DateTime PublishedDate { get; set; }
        public string SourceText { get; set; }
        public string SourceTextVersion { get; set; }
        public string Version { get; set; }
        public string Book { get; set; }
        /// <summary>
        /// Whether or not this is bible or obs
        /// </summary>
        public string Type { get; set; }
        public string BookName { get; set; }
        public string Title { get; set; }
        public string LanguageName { get; set; }
        public string LanguageDirection { get; set; }
    }
}
