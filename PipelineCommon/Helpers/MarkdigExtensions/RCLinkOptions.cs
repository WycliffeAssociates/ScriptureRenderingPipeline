using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipelineCommon.Helpers.MarkdigExtensions
{
    public class RCLinkOptions
    {
        public string ServerUrl { get; set; }
        public string BaseUser { get; set; }
        public string LanguageCode { get; set; }
        public bool RenderAsBTTWriterLinks { get; set; }
        public RCLinkOptions()
        {
        }
    }
}
