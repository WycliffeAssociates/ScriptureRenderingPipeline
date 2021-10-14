using Newtonsoft.Json;
using System.Collections.Generic;

namespace BTTWriterCatalog.Models.BIELCatalog
{
    internal class ResourceCheckingInformation
    {
        [JsonProperty("checking_entity")]
        public List<string> CheckingEntities {  get; set; }
        [JsonProperty("checking_level")]
        public string CheckingLevel { get; set; }
    }
}
