using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptureRenderingPipeline.Models
{
    internal class TranslationDatabaseLanguage
    {
        [JsonProperty("cc")]
        public List<string> Countries { get; set; }
        [JsonProperty("ln")]
        public string LanguageName { get; set; }
        [JsonProperty("pk")]
        public int TranslationDatabaseId {  get; set;  }
        [JsonProperty("ld")]
        public string Direction { get; set; }
        [JsonProperty("lc")]
        public string LanguageCode { get; set; }
        [JsonProperty("alt")]
        public List<string> AlternateNames { get; set; }
        [JsonProperty("gw")]
        public bool IsGateway { get; set; }
        [JsonProperty("ang")]
        public string AnglicizedName { get; set; }
    }
}
