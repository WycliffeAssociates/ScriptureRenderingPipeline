using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTTWriterCatalog.Models.WriterCatalog
{
    class CatalogBook
    {
        public string date_modified { get; set; }
        public string lang_catalog { get; set; }
        public string[] meta { get; set; }
        public string slug { get; set; }
        public string sort { get; set; }
    }

}
