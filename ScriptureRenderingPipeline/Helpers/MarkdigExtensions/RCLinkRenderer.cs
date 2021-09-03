using Markdig.Renderers;
using Markdig.Renderers.Html;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptureRenderingPipeline.Helpers.MarkdigExtensions
{
    public class RCLinkRenderer : HtmlObjectRenderer<RCLink>
    {
        private readonly RCLinkOptions _options;
        public RCLinkRenderer(RCLinkOptions options)
        {
            _options = options; 
        }

        protected override void Write(HtmlRenderer renderer, RCLink obj)
        {
            if (renderer.EnableHtmlForInline)
            {
                string link = GenerateLink(obj);
                renderer.Write("<a href=\"").Write(link).Write("\">").Write(link).Write("</a>");
            }
        }

        public string GenerateLink(RCLink input)
        {
            var rawLink = input.Link.ToString();
            
            // HACK: If the link doesn't contain slashes, for now ignore it.
            if (!rawLink.Contains("/"))
            {
                return rawLink;
            }

            var splitLink = rawLink.Split("/");
            var language = splitLink[2];
            var resource = splitLink[3];
            if (_options.ResourceOverrideMapping.ContainsKey(resource))
            {
                resource = _options.ResourceOverrideMapping[resource];
            }
            var path = string.Join("/", splitLink[5..]);
            return $"{_options.ServerUrl}/{_options.BaseUser}/{language}_{resource}/src/branch/master/{path}";
        }
    }
}
