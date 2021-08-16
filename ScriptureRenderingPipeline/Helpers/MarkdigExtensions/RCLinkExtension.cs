using Markdig;
using Markdig.Parsers.Inlines;
using Markdig.Renderers;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptureRenderingPipeline.Helpers.MarkdigExtensions
{
    public class RCLinkExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
            if (!pipeline.InlineParsers.Contains<RCLinksParser>())
            {
                pipeline.InlineParsers.Insert(0, new RCLinksParser());
            }
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            // Nothing needed since this will just be using the built in link functionality
        }
    }
}
