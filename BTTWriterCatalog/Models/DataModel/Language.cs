using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTTWriterCatalog.Models.DataModel
{
    public class Language
    {
        [JsonProperty("id")]
        public string Slug { get; set;  }
        public string Partition => "partition";
        public string Name {  get; set; }
        public string Direction { get; set; }
    }
}
