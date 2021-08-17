using Markdig.Helpers;
using Markdig.Syntax.Inlines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptureRenderingPipeline.Helpers.MarkdigExtensions
{
    public class RCLink : LeafInline
    {
        public StringSlice Link { get; set; }
    }
}
