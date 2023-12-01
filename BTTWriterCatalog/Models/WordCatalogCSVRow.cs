using CsvHelper.Configuration.Attributes;

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
