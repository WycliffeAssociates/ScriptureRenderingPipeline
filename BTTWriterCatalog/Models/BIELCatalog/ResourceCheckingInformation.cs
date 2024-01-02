using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.BIELCatalog
{
    internal class ResourceCheckingInformation
    {
       [JsonPropertyName("checking_entity")]
        public List<string> CheckingEntities {  get; set; }
       [JsonPropertyName("checking_level")]
        public string CheckingLevel { get; set; }
    }
}
