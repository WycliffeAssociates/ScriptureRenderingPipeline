using Markdig;
using NUnit.Framework;
using ScriptureRenderingPipeline.Helpers.MarkdigExtensions;
using ScriptureRenderingPipeline.Renderers;
using System.Collections.Generic;

namespace SRPTests
{
    public class RCLinkTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            RCLinkOptions options = new RCLinkOptions()
            {
                BaseUser = "WycliffeAssociates",
                ResourceOverrideMapping = new Dictionary<string, string>()
                {
                    ["ta"] = "tm"
                },
                // TODO: this needs to be changed to a configuration value
                ServerUrl = "https://content.bibletranslationtools.org"
            };
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Use<RCLinkExtension>(new RCLinkExtension(options)).Build();
            var ast = Markdown.Parse("[[rc://en/ta/man/translate/translate-dynamic]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url = "/WycliffeAssociates/en_tm/src/branch/master/translate/translate-dynamic";
            var expected_html = $"<p><a href=\"{options.ServerUrl}{expected_url}\">{options.ServerUrl}{expected_url}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }
    }
}