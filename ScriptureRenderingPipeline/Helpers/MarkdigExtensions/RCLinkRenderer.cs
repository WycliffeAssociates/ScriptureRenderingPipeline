using Markdig.Renderers;
using Markdig.Renderers.Html;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ScriptureRenderingPipeline.Helpers.MarkdigExtensions
{
    public class RCLinkRenderer : HtmlObjectRenderer<RCLink>
    {
        private static Regex TA_LINK = new Regex(@"rc:\/\/([^\/]+)\/ta\/man\/([^]]+)", RegexOptions.Compiled);
        private static Regex TN_LINK = new Regex(@"rc:\/\/([^\/]+)\/tn\/([^]]+)", RegexOptions.Compiled);
        private static Regex TQ_LINK = new Regex(@"rc:\/\/([^\/]+)\/tq\/([^]]+)", RegexOptions.Compiled);
        private static Regex TW_LINK = new Regex(@"rc:\/\/([^\/]+)\/tw\/dict\/([^]]+)", RegexOptions.Compiled);

        private readonly RCLinkOptions _options;
        public RCLinkRenderer(RCLinkOptions options)
        {
            _options = options;
        }

        protected override void Write(HtmlRenderer renderer, RCLink obj)
        {
            if (!renderer.EnableHtmlForInline)
            {
                return;
            }

            string linktext = obj.Link.ToString();
            string url = "";
            Match match;

            match = TA_LINK.Match(linktext);
            if (match.Success)
            {
                var language = match.Groups[1];
                var path = match.Groups[2];
                var link = $"{_options.ServerUrl}/{_options.BaseUser}/{language}_tm/src/branch/master/{path}";
                renderer.Write("<a href=\"").Write(link).Write("\">").Write(link).Write("</a>");
                return;
            }

            match = TN_LINK.Match(linktext);
            if (match.Success)
            {
                var language = match.Groups[1];
                var path = match.Groups[2];
                var link = $"{_options.ServerUrl}/{_options.BaseUser}/{language}_tn/src/branch/master/{path}";
                renderer.Write("<a href=\"").Write(link).Write("\">").Write(link).Write("</a>");
                return;
            }

            match = TQ_LINK.Match(linktext);
            if (match.Success)
            {
                var language = match.Groups[1];
                var path = match.Groups[2];
                var link = $"{_options.ServerUrl}/{_options.BaseUser}/{language}_tq/src/branch/master/{path}";
                renderer.Write("<a href=\"").Write(link).Write("\">").Write(link).Write("</a>");
                return;
            }

            match = TW_LINK.Match(linktext);
            if (match.Success)
            {
                var language = match.Groups[1];
                var path = match.Groups[2];
                var link = $"{_options.ServerUrl}/{_options.BaseUser}/{language}_tw/src/branch/master/{path}";
                renderer.Write("<a href=\"").Write(link).Write("\">").Write(link).Write("</a>");
                return;
            }

            // No match
            renderer.Write(linktext);
        }

        //public string GenerateLink(RCLink input)
        //{
        //    var rawLink = input.Link.ToString();

        //    // HACK: If the link doesn't contain slashes, for now ignore it.
        //    if (!rawLink.Contains("/"))
        //    {
        //        return rawLink;
        //    }

        //    var splitLink = rawLink.Split("/");
        //    var language = splitLink[2];
        //    var resource = splitLink[3];
        //    if (_options.ResourceOverrideMapping.ContainsKey(resource))
        //    {
        //        resource = _options.ResourceOverrideMapping[resource];
        //    }
        //    var path = string.Join("/", splitLink[5..]);
        //    return $"{_options.ServerUrl}/{_options.BaseUser}/{language}_{resource}/src/branch/master/{path}";
        //}
    }
}
