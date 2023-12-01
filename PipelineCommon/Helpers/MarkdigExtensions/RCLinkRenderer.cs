using Markdig.Renderers;
using Markdig.Renderers.Html;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PipelineCommon.Helpers.MarkdigExtensions
{
    public class RCLinkRenderer : HtmlObjectRenderer<RCLink>
    {
        private static Regex TA_LINK = new Regex(@"rc:\/\/([^\/]+)\/(ta|tm)\/man\/([^/]+)\/([^]]+)", RegexOptions.Compiled);
        private static Regex TN_TQ_LINK = new Regex(@"rc:\/\/([^\/]+)\/(tn|tq)\/([^/]+)\/([^/]+)\/([^].]+)", RegexOptions.Compiled);
        private static Regex TW_LINK = new Regex(@"rc:\/\/([^\/]+)\/tw\/dict\/bible\/([^/]+)\/([^].]+)", RegexOptions.Compiled);
        private static Regex BIBLE_LINK = new Regex(@"rc:\/\/([^\/]+)\/bible\/(\w*)\/(\w+)\/([\d+]+)\/(\d+)", RegexOptions.Compiled);

        private readonly RCLinkOptions _options;
        public RCLinkRenderer(RCLinkOptions options)
        {
            _options = options;
        }

        protected override void Write(HtmlRenderer renderer, RCLink obj)
        {
            if (!renderer.EnableHtmlForInline && !_options.RenderAsBTTWriterLinks)
            {
                return;
            }

            string rcLinkText = obj.Link.ToString();
            Match match;

            match = TA_LINK.Match(rcLinkText);
            if (match.Success)
            {
                var language = match.Groups[1].Value;
                if (language == "*")
                {
                    language = _options.LanguageCode;
                }
                // Group 2 is the resource: ta or tm.
                // We don't care which since we always output tm.
                var page = match.Groups[3];
                var topic = match.Groups[4];
                if (_options.RenderAsBTTWriterLinks)
                {
                    RenderBTTWriterLink(renderer, $":{language}:ta:vol2:{page}:{topic}");
                    return;
                }
                var tmp = new Dictionary<string, string>()
                {
                    ["repo"] = $"{language}_tm",
                    ["user"] = _options.BaseUser,
                    ["page"] = page.ToString(),
                    ["topic"] = topic.ToString(),
                    ["type"] = "tm"
                };
                RenderLink(renderer, $"{_options.ServerUrl}/u/{_options.BaseUser}/{language}_tm/{page}.html#{topic}", tmp);
                return;
            }

            match = TN_TQ_LINK.Match(rcLinkText);
            if (match.Success)
            {
                var language = match.Groups[1].Value;
                if (language == "*")
                {
                    language = _options.LanguageCode;
                }
                var resource = match.Groups[2];
                var book = match.Groups[3];
                var bookUpper = book.ToString().ToUpper();
                var bookNum = Utils.GetBookNumber(bookUpper);
                var chapter = match.Groups[4];
                var verse = match.Groups[5];
                if (_options.RenderAsBTTWriterLinks)
                {
                    RenderBTTWriterLink(renderer, rcLinkText);
                    return;
                }
                var tmp = new Dictionary<string, string>()
                {
                    ["repo"] = $"{language}_{resource}",
                    ["user"] = _options.BaseUser,
                    ["book"] = book.ToString(),
                    ["chapter"] = chapter.ToString(),
                    ["verse"] = verse.ToString(),
                    ["type"] = resource.ToString()
                };
                RenderLink(renderer, $"{_options.ServerUrl}/u/{_options.BaseUser}/{language}_{resource}/{bookNum}-{bookUpper}.html#{resource}-chunk-{book}-{chapter}-{verse}", tmp);
                return;
            }

            match = TW_LINK.Match(rcLinkText);
            if (match.Success)
            {
                var language = match.Groups[1].Value;
                if (language == "*")
                {
                    language = _options.LanguageCode;
                }
                var page = match.Groups[2];
                var topic = match.Groups[3];
                if (_options.RenderAsBTTWriterLinks)
                {
                    RenderBTTWriterLink(renderer, rcLinkText);
                    return;
                }
                var tmp = new Dictionary<string, string>()
                {
                    ["repo"] = $"{language}_tw",
                    ["user"] = _options.BaseUser,
                    ["category"] = page.ToString(),
                    ["word"] = topic.ToString(),
                    ["type"] = "tw"
                };
                RenderLink(renderer, $"{_options.ServerUrl}/u/{_options.BaseUser}/{language}_tw/{page}.html#{topic}", tmp);
                return;
            }

            match = BIBLE_LINK.Match(rcLinkText);
            if (match.Success)
            {
                var language = match.Groups[1].Value;
                if (language == "*")
                {
                    language = _options.LanguageCode;
                }

                var bibleVersion = match.Groups[2];
                var book = match.Groups[3];
                var chapter = match.Groups[4];
                var verse = match.Groups[5];

                if (_options.RenderAsBTTWriterLinks)
                {
                    RenderBTTWriterLink(renderer, $":{language}:bible:{bibleVersion}:{book}:{chapter}:{verse}|");// The | is nessecary to match the regex in writer. What does it do? Absolutely nothing.
                    return;
                }

                var tmp = new Dictionary<string, string>()
                {
                    ["repo"] = $"{language}_{bibleVersion}",
                    ["user"] = _options.BaseUser,
                    ["book"] = book.ToString(),
                    ["chapter"] = chapter.ToString(),
                    ["verse"] = verse.ToString(),
                    ["type"] = "bible"
                };
                RenderLink(renderer, $"{_options.ServerUrl}/u/{_options.BaseUser}/{language}_{bibleVersion}/{book}.html#chp-{chapter}-vs-{verse}", tmp);
                return;
            }

            // We didn't find a link.  Render the raw text.
            // TODO: Can we see the log from here?  If so, write to it
            renderer.Write(rcLinkText);

        }

        private void RenderLink(HtmlRenderer renderer, string htmlLink, Dictionary<string, string> dataAttributes = null)
        {
            var tmp = dataAttributes == null ? string.Empty : string.Join(" ", dataAttributes.Select(i => $"data-{i.Key}=\"{i.Value}\""));
            renderer.Write("<a href=\"").Write(htmlLink).Write($"\" target=\"_blank\" data-is-rc-link {tmp}>").Write(htmlLink).Write("</a>");
        }
        private void RenderBTTWriterLink(HtmlRenderer renderer, string link)
        {
            renderer.Write($"[[{link}]]");
        }

    }
}
