using Markdig;
using Markdig.Parsers.Inlines;
using Markdig.Renderers;

namespace PipelineCommon.Helpers.MarkdigExtensions
{
    public class RCLinkExtension : IMarkdownExtension
    {
        private readonly RCLinkOptions _options;

        public RCLinkExtension(RCLinkOptions options)
        {
            _options = options;
        }
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
            if (!pipeline.InlineParsers.Contains<RCLinksParser>())
            {
                pipeline.InlineParsers.InsertBefore<LinkInlineParser>(new RCLinksParser());
            }
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            var htmlRenderer = renderer as HtmlRenderer;
            var renderers = htmlRenderer?.ObjectRenderers;
            if (renderers != null && !renderers.Contains<RCLinkRenderer>())
            {
                renderers.Add(new RCLinkRenderer(_options));
            }
        }
    }
}
