using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptureRenderingPipeline.Helpers.MarkdigExtensions
{
    public class RCLinkOptions
    {
        public string ServerUrl { get; set; }
        public string BaseUser { get; set; }
        public Dictionary<string,string> ResourceOverrideMapping { get; set; }
        public RCLinkOptions()
        {
            ResourceOverrideMapping = new Dictionary<string,string>();
        }
    }
}
