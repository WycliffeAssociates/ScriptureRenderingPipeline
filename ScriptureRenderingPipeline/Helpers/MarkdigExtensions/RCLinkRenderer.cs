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
        private static Regex TA_LINK = new Regex(@"rc:\/\/([^\/]+)\/(ta|tm)\/man\/([^/]+)\/([^]]+)", RegexOptions.Compiled);
        private static Regex TN_TQ_LINK = new Regex(@"rc:\/\/([^\/]+)\/(tn|tq)\/([^/]+)\/([^/]+)\/([^]]+).md", RegexOptions.Compiled);
        private static Regex TW_LINK = new Regex(@"rc:\/\/([^\/]+)\/tw\/dict\/bible\/([^/]+)\/([^]]+).md", RegexOptions.Compiled);

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

            string rcLinkText = obj.Link.ToString();
            Match match;

            match = TA_LINK.Match(rcLinkText);
            if (match.Success)
            {
                var language = match.Groups[1];
                // Group 2 is the resource: ta or tm.
                // We don't care which since we always output tm.
                var page = match.Groups[3];
                var topic = match.Groups[4];
                renderLink(renderer, $"{_options.ServerUrl}/{_options.BaseUser}/{language}_tm/{page}.html#{topic}");
                return;
            }

            match = TN_TQ_LINK.Match(rcLinkText);
            if (match.Success)
            {
                var language = match.Groups[1];
                var resource = match.Groups[2];
                var book = match.Groups[3];
                var bookUpper = book.ToString().ToUpper();
                var bookNum = PipelineCommon.Helpers.Utils.GetBookNumber(bookUpper);
                var chapter = match.Groups[4];
                var verse = match.Groups[5];
                renderLink(renderer, $"{_options.ServerUrl}/{_options.BaseUser}/{language}_{resource}/{bookNum}-{bookUpper}.html#{resource}-chunk-{book}-{chapter}-{verse}");
                return;
            }

            match = TW_LINK.Match(rcLinkText);
            if (match.Success)
            {
                var language = match.Groups[1];
                var page = match.Groups[2];
                var topic = match.Groups[3];
                renderLink(renderer, $"{_options.ServerUrl}/{_options.BaseUser}/{language}_tw/{page}.html#{topic}");
                return;
            }

            // We didn't find a link.  Render the raw text.
            // TODO: Can we see the log from here?  If so, write to it
            renderer.Write(rcLinkText);

        }

        private void renderLink(HtmlRenderer renderer, string htmlLink)
        {
            renderer.Write("<a href=\"").Write(htmlLink).Write("\">").Write(htmlLink).Write("</a>");
        }

    }
}
