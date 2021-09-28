using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace BTTWriterCatalog.Models
{
    public class WordCatalogCSVRow
    {
        public string Book { get; set; }
        public int Chapter { get; set; }
        public int Verse { get; set; }
        [Name("Ref")]
        public string Word { get; set; }
        public string Term { get; set; }
        [Name("Dir")]
        public string Directory { get; set; }
    }
}
