using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTTWriterCatalog.Models.WriterCatalog
{
    public class WriterCatalogResource
    {
        public string checking_questions { get; set; }
        public string chunks { get; set; }
        public string date_modified { get; set; }
        public string name { get; set; }
        public string notes { get; set; }
        public string slug { get; set; }
        public string source { get; set; }
        public Status status { get; set; }
        public string terms { get; set; }
        public string tw_cat { get; set; }
        public string usfm { get; set; }
    }

}
