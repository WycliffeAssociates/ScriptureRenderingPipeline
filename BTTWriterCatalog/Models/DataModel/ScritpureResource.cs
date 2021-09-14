using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTTWriterCatalog.Models.DataModel
{
    class ScritpureResource
    {
        public string Language { get; set; }
        public string Name { get; set; }
        public DateTime ModifiedOn { get; set; }
        public DateTime CreatedOn {  get; set; }
        public string CheckingEntity { get; set; }
        public string CheckingLevel { get; set; }
        public string Comments { get; set; }
        public List<string> Contributors { get; set; }
        public DateTime PublishedDate { get; set; }
        public string SourceText { get; set; }
        public string SourceTextVersion { get; set; }
        public string Version { get; set; }
        /// <summary>
        /// Whether or not this is bible or obs
        /// </summary>
        public string Type { get; set; }
    }
}
