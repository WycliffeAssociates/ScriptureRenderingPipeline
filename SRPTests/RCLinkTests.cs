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
                ServerUrl = "https://content.bibletranslationtools.org"
            };
            this.pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Use<RCLinkExtension>(new RCLinkExtension(options)).Build();
        }

        [Test]
        public void TestTaLink()
        {
            var ast = Markdown.Parse("[[rc://en/ta/man/translate/translate-dynamic]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url = "/WycliffeAssociates/en_tm/translate.html#translate-dynamic";
            var expected_html = $"<p><a href=\"{this.options.ServerUrl}{expected_url}\">{this.options.ServerUrl}{expected_url}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }

        [Test]
        public void TestEmbeddedTaLink()
        {
            var ast = Markdown.Parse("Here is a link: [[rc://en/ta/man/translate/translate-dynamic]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url = "/WycliffeAssociates/en_tm/translate.html#translate-dynamic";
            var expected_html = $"<p>Here is a link: <a href=\"{this.options.ServerUrl}{expected_url}\">{this.options.ServerUrl}{expected_url}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }

        [Test]
        public void TestMultipleEmbeddedTaLinks()
        {
            var ast = Markdown.Parse("See also [[rc://en/ta/man/jit/figs-hyperbole]] and [[rc://en/ta/man/cobt/gl-strategy]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url1 = "/WycliffeAssociates/en_tm/jit.html#figs-hyperbole";
            var expected_url2 = "/WycliffeAssociates/en_tm/cobt.html#gl-strategy";
            var expected_html = $"<p>See also <a href=\"{this.options.ServerUrl}{expected_url1}\">{this.options.ServerUrl}{expected_url1}</a> and <a href=\"{this.options.ServerUrl}{expected_url2}\">{this.options.ServerUrl}{expected_url2}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }

        [Test]
        public void TestTmLink()
        {
            var ast = Markdown.Parse("[[rc://en/tm/man/translate/translate-dynamic]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url = "/WycliffeAssociates/en_tm/translate.html#translate-dynamic";
            var expected_html = $"<p><a href=\"{this.options.ServerUrl}{expected_url}\">{this.options.ServerUrl}{expected_url}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }

        [Test]
        public void TestTnLink()
        {
            var ast = Markdown.Parse("[[rc://en/tn/php/04/08.md]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url = "/WycliffeAssociates/en_tn/51-PHP.html#tn-chunk-php-04-08";
            var expected_html = $"<p><a href=\"{this.options.ServerUrl}{expected_url}\">{this.options.ServerUrl}{expected_url}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }


        [Test]
        public void TestTQLink()
        {
            var ast = Markdown.Parse("[[rc://en/tq/mat/24/45.md]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url = "/WycliffeAssociates/en_tq/41-MAT.html#tq-chunk-mat-24-45";
            var expected_html = $"<p><a href=\"{this.options.ServerUrl}{expected_url}\">{this.options.ServerUrl}{expected_url}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }

        [Test]
        public void TestTwLink()
        {
            var ast = Markdown.Parse("[[rc://en/tw/dict/bible/kt/altar.md]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url = "/WycliffeAssociates/en_tw/kt.html#altar";
            var expected_html = $"<p><a href=\"{this.options.ServerUrl}{expected_url}\">{this.options.ServerUrl}{expected_url}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }


        [Test]
        public void TestBadLink()
        {
            var ast = Markdown.Parse("[[rc://this/is/a/bad/link.md]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_html = $"<p>rc://this/is/a/bad/link.md</p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }

    }
}