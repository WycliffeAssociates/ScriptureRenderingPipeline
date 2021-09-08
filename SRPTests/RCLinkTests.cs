using Markdig;
using NUnit.Framework;
using ScriptureRenderingPipeline.Helpers.MarkdigExtensions;
using ScriptureRenderingPipeline.Renderers;
using System.Collections.Generic;

namespace SRPTests
{
    public class RCLinkTests
    {
        private RCLinkOptions options;
        private MarkdownPipeline pipeline;

        [SetUp]
        public void Setup()
        {
            this.options = new RCLinkOptions()
            {
                BaseUser = "WycliffeAssociates",
                ResourceOverrideMapping = new Dictionary<string, string>()
                {
                    ["ta"] = "tm"
                },
                // TODO: this needs to be changed to a configuration value
                ServerUrl = "https://content.bibletranslationtools.org"
            };
            this.pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Use<RCLinkExtension>(new RCLinkExtension(options)).Build();
        }

        [Test]
        public void TestTMLink()
        {
            var ast = Markdown.Parse("[[rc://en/ta/man/translate/translate-dynamic]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url = "/WycliffeAssociates/en_tm/src/branch/master/translate/translate-dynamic";
            var expected_html = $"<p><a href=\"{this.options.ServerUrl}{expected_url}\">{this.options.ServerUrl}{expected_url}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }
    }
}