using Markdig;
using NUnit.Framework;
using PipelineCommon.Helpers.MarkdigExtensions;
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
                ServerUrl = "https://content.bibletranslationtools.org",
                LanguageCode = "en"
            };
            this.pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Use(new RCLinkExtension(options)).Build();
        }

        [Test]
        public void TestTaLink()
        {
            var ast = Markdown.Parse("[[rc://en/ta/man/translate/translate-dynamic]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url = "/u/WycliffeAssociates/en_tm/translate.html#translate-dynamic";
            var expected_html = $"<p><a href=\"{this.options.ServerUrl}{expected_url}\" target=\"_blank\" data-is-rc-link data-repo=\"en_tm\" data-user=\"{options.BaseUser}\" data-page=\"translate\" data-topic=\"translate-dynamic\" data-type=\"tm\">{this.options.ServerUrl}{expected_url}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }

        [Test]
        public void TestEmbeddedTaLink()
        {
            var ast = Markdown.Parse("Here is a link: [[rc://en/ta/man/translate/translate-dynamic]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url = "/u/WycliffeAssociates/en_tm/translate.html#translate-dynamic";
            var expected_html = $"<p>Here is a link: <a href=\"{this.options.ServerUrl}{expected_url}\" target=\"_blank\" data-is-rc-link data-repo=\"en_tm\" data-user=\"{options.BaseUser}\" data-page=\"translate\" data-topic=\"translate-dynamic\" data-type=\"tm\">{this.options.ServerUrl}{expected_url}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }

        [Test]
        public void TestMultipleEmbeddedTaLinks()
        {
            var ast = Markdown.Parse("See also [[rc://en/ta/man/jit/figs-hyperbole]] and [[rc://en/ta/man/cobt/gl-strategy]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url1 = "/u/WycliffeAssociates/en_tm/jit.html#figs-hyperbole";
            var expected_url2 = "/u/WycliffeAssociates/en_tm/cobt.html#gl-strategy";
            var expected_html = $"<p>See also <a href=\"{this.options.ServerUrl}{expected_url1}\" target=\"_blank\" data-is-rc-link data-repo=\"en_tm\" data-user=\"{options.BaseUser}\" data-page=\"jit\" data-topic=\"figs-hyperbole\" data-type=\"tm\">{this.options.ServerUrl}{expected_url1}</a> and <a href=\"{this.options.ServerUrl}{expected_url2}\" target=\"_blank\" data-is-rc-link data-repo=\"en_tm\" data-user=\"{options.BaseUser}\" data-page=\"cobt\" data-topic=\"gl-strategy\" data-type=\"tm\">{this.options.ServerUrl}{expected_url2}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }

        [Test]
        public void TestTmLink()
        {
            var ast = Markdown.Parse("[[rc://en/tm/man/translate/translate-dynamic]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url = "/u/WycliffeAssociates/en_tm/translate.html#translate-dynamic";
            var expected_html = $"<p><a href=\"{this.options.ServerUrl}{expected_url}\" target=\"_blank\" data-is-rc-link data-repo=\"en_tm\" data-user=\"{options.BaseUser}\" data-page=\"translate\" data-topic=\"translate-dynamic\" data-type=\"tm\">{this.options.ServerUrl}{expected_url}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }

        [Test]
        public void TestTnLink()
        {
            var ast = Markdown.Parse("[[rc://en/tn/php/04/08.md]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url = "/u/WycliffeAssociates/en_tn/51-PHP.html#tn-chunk-php-04-08";
            var expected_html = $"<p><a href=\"{this.options.ServerUrl}{expected_url}\" target=\"_blank\" data-is-rc-link data-repo=\"en_tn\" data-user=\"{options.BaseUser}\" data-book=\"php\" data-chapter=\"04\" data-verse=\"08\" data-type=\"tn\">{this.options.ServerUrl}{expected_url}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }


        [Test]
        public void TestTQLink()
        {
            var ast = Markdown.Parse("[[rc://en/tq/mat/24/45.md]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url = "/u/WycliffeAssociates/en_tq/41-MAT.html#tq-chunk-mat-24-45";
            var expected_html = $"<p><a href=\"{this.options.ServerUrl}{expected_url}\" target=\"_blank\" data-is-rc-link data-repo=\"en_tq\" data-user=\"{options.BaseUser}\" data-book=\"mat\" data-chapter=\"24\" data-verse=\"45\" data-type=\"tq\">{this.options.ServerUrl}{expected_url}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }

        [Test]
        public void TestTwLink()
        {
            var ast = Markdown.Parse("[[rc://en/tw/dict/bible/kt/altar.md]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url = "/u/WycliffeAssociates/en_tw/kt.html#altar";
            var expected_html = $"<p><a href=\"{this.options.ServerUrl}{expected_url}\" target=\"_blank\" data-is-rc-link data-repo=\"en_tw\" data-user=\"{options.BaseUser}\" data-category=\"kt\" data-word=\"altar\" data-type=\"tw\">{this.options.ServerUrl}{expected_url}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }

        [Test]
        public void TestStarTwLink()
        {
            var ast = Markdown.Parse("[[rc://en/tw/dict/bible/kt/altar.md]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url = $"/u/WycliffeAssociates/{this.options.LanguageCode}_tw/kt.html#altar";
            var expected_html = $"<p><a href=\"{this.options.ServerUrl}{expected_url}\" target=\"_blank\" data-is-rc-link data-repo=\"en_tw\" data-user=\"WycliffeAssociates\" data-category=\"kt\" data-word=\"altar\" data-type=\"tw\">{this.options.ServerUrl}{expected_url}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }

        [Test]
        public void TestBibleLink()
        {
            var ast = Markdown.Parse("[[rc://en/bible/ulb/gen/1/1]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url = $"/u/WycliffeAssociates/{this.options.LanguageCode}_ulb/gen.html#chp-1-vs-1";
            var expected_html = $"<p><a href=\"{this.options.ServerUrl}{expected_url}\" target=\"_blank\" data-is-rc-link data-repo=\"en_ulb\" data-user=\"{options.BaseUser}\" data-book=\"gen\" data-chapter=\"1\" data-verse=\"1\" data-type=\"bible\">{this.options.ServerUrl}{expected_url}</a></p>\n";
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

        [Test]
        public void TestBibleBTTWriterLinks()
        {
            this.options.RenderAsBTTWriterLinks = true;
            this.pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Use(new RCLinkExtension(options)).Build();
            var ast = Markdown.Parse("[[rc://en/bible/ulb/gen/1/1]]", pipeline);
            var actualHtml = ast.ToHtml(pipeline);
            var expectedHtml = $"<p>[[:{this.options.LanguageCode}:bible:ulb:gen:1:1|]]</p>\n";
            Assert.AreEqual(expectedHtml, actualHtml);
        }
        [Test]
        public void TestStarBibleLanguageLink()
        {
            var ast = Markdown.Parse("[[rc://*/bible/ulb/gen/1/1]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url = $"/u/WycliffeAssociates/{this.options.LanguageCode}_ulb/gen.html#chp-1-vs-1";
            var expected_html = $"<p><a href=\"{this.options.ServerUrl}{expected_url}\" target=\"_blank\" data-is-rc-link data-repo=\"en_ulb\" data-user=\"{options.BaseUser}\" data-book=\"gen\" data-chapter=\"1\" data-verse=\"1\" data-type=\"bible\">{this.options.ServerUrl}{expected_url}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }
        
        [Test]
        public void TestStarTnTqLink()
        {
            var ast = Markdown.Parse("[[rc://*/tn/php/04/08.md]]", pipeline);
            var actual_html = Markdown.ToHtml(ast, pipeline);
            var expected_url = "/u/WycliffeAssociates/en_tn/51-PHP.html#tn-chunk-php-04-08";
            var expected_html = $"<p><a href=\"{this.options.ServerUrl}{expected_url}\" target=\"_blank\" data-is-rc-link data-repo=\"en_tn\" data-user=\"{options.BaseUser}\" data-book=\"php\" data-chapter=\"04\" data-verse=\"08\" data-type=\"tn\">{this.options.ServerUrl}{expected_url}</a></p>\n";
            Assert.AreEqual(expected_html, actual_html);
        }

    }
}