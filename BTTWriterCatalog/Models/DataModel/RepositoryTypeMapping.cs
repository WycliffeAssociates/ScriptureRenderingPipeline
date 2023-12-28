
using System.Text.Json.Serialization;

namespace BTTWriterCatalog.Models.DataModel
{
    internal class RepositoryTypeMapping
    {
        public string id => $"{User}_{Repository}";
        public string Partition => "Partition";
        public string User { get; set; }
        public string Repository { get; set; }
        public string Type { get; set; }
        public string Language { get; set; }
    }
}
