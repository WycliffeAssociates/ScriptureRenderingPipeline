using System;
using System.Collections.Generic;
using System.Text;

namespace PipelineCommon.Models.ResourceContainer
{
    public class DublinCore
    {
        public string conformsto { get; set; }
        public string[] contributor { get; set; }
        public string creator { get; set; }
        public string description { get; set; }
        public string format { get; set; } 
        public string identifier { get; set; }
        public string issued { get; set; }
        public Language language { get; set; }
        public string modified { get; set; }
        public string publisher { get; set; }
        public string[] relation { get; set; }
        public string rights { get; set; }
        public string subject { get; set; }
        public string title { get; set; }
        public string type { get; set; }
        public string version { get; set; }
        public Source[] source { get; set; }
    }
}
