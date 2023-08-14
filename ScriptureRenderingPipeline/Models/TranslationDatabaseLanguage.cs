using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ScriptureRenderingPipeline.Models
{
    internal class TranslationDatabaseLanguage
    {
        [JsonPropertyName("cc")]
        public List<string> Countries { get; set; }
        [JsonPropertyName("ln")]
        public string LanguageName { get; set; }
        [JsonPropertyName("pk")]
        public int TranslationDatabaseId {  get; set;  }
        [JsonPropertyName("ld")]
        public string Direction { get; set; }
        [JsonPropertyName("lc")]
        public string LanguageCode { get; set; }
        [JsonPropertyName("alt")]
        public List<string> AlternateNames { get; set; }
        [JsonPropertyName("gw")]
        public bool IsGateway { get; set; }
        [JsonPropertyName("ang")]
        public string AnglicizedName { get; set; }
    }
}
