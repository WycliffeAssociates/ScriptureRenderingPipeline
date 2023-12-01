using Markdig.Helpers;
using Markdig.Syntax.Inlines;

namespace PipelineCommon.Helpers.MarkdigExtensions
{
    public class RCLink : LeafInline
    {
        public StringSlice Link { get; set; }
    }
}
