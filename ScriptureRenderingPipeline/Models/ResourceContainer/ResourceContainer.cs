using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptureRenderingPipeline.Models.ResourceContainer
{
    class ResourceContainer
    {
        public DublinCore dublin_core { get; set; }
        public Checking checking { get; set; }
        public Project[] projects { get; set; }
    }
}
