using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BTTWriterCatalog.Models
{
    public class InputChunk
    {
        [JsonProperty("chp")]
        public string Chapter {  get; set; }
        [JsonProperty("firstvs")]
        public string FirstVerse { get; set; }
    }
}
