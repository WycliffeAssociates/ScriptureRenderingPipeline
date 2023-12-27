using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BTTWriterCatalog.Models
{
    public class Door43Chunk
    {
       [JsonPropertyName("chp")]
        public string Chapter {  get; set; }
       [JsonPropertyName("firstvs")]
        public string FirstVerse { get; set; }
    }
}
