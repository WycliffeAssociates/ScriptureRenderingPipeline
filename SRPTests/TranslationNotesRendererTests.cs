using System.Text.Json;
using System.Threading.Tasks;
using DotLiquid;
using NUnit.Framework;
using ScriptureRenderingPipeline.Models;
using ScriptureRenderingPipeline.Renderers;
using SRPTests.TestHelpers;

namespace SRPTests;

public class TranslationNotesRendererTests
{
        private const string InputMarkdown = """
                                             # Notes about Genesis 1:1
                                             
                                             The beginning was the beginning
                                             """;
        private const string FrontMatterMarkdown = "# Intro to Genesis";
    
        private const string FrontMatterExpected = """
                                                   <div id="tn-chapter-gen-front"></div>
                                                   <div id="tn-chunk-gen-front-intro"></div>
                                                   <h2 id="intro-to-genesis">Intro to Genesis</h2>
                                                   
                                                   
                                                   """;
        private const string ExpectedResult = """
                                              <h1 id="tn-chapter-gen-01">Genesis 1</h2>
                                              <h1 id="tn-chunk-gen-01-01">Genesis 1:1</h2>
                                              <h2 id="notes-about-genesis-11">Notes about Genesis 1:1</h2>
                                              <p>The beginning was the beginning</p>
                                              
                                              
                                              """;
        [Test]
        public async Task TestEmpty()
        {
            var outputFileSystem = new FakeOutputInterface();
            var inputFileSystem = new FakeZipFileSystem();
            var input = new RendererInput()
            {
                FileSystem = inputFileSystem,
                PrintTemplate = Template.Parse("{{ content }}")
            };
            var renderer = new TranslationNotesRenderer();
            await renderer.RenderAsync(input, outputFileSystem);
            Assert.AreEqual(2, outputFileSystem.Files.Count);
        }
        [Test]
        public async Task TestWithContent()
        {
            var outputFileSystem = new FakeOutputInterface();
            var inputFileSystem = new FakeZipFileSystem();
            inputFileSystem.AddFolder("base/gen");
            inputFileSystem.AddFolder("base/gen/01");
            inputFileSystem.AddFolder("base/gen/front");
            inputFileSystem.AddFile("base/gen/01/01.md", InputMarkdown);
            inputFileSystem.AddFile("base/gen/front/intro.md", FrontMatterMarkdown);
            var input = new RendererInput()
            {
                FileSystem = inputFileSystem,
                PrintTemplate = Template.Parse("{{ content }}"),
                BasePath = "base",
                LanguageCode = "fr",
                LanguageName = "French",
                LanguageTextDirection = "ltr",
                RepoUrl = "https://content.bibletranslationtools.org/WA-Catalog/fr_tq",
                Title = "French Translation Notes"
            };
            var renderer = new TranslationNotesRenderer();
            await renderer.RenderAsync(input, outputFileSystem);
            Assert.AreEqual(ExpectedResult, outputFileSystem.Files["gen/01.html"]);
            Assert.AreEqual(FrontMatterExpected, outputFileSystem.Files["gen/front.html"]);
            
            var index = JsonSerializer.Deserialize<OutputIndex>(outputFileSystem.Files["index.json"]);
            
            Assert.AreEqual(input.LanguageCode, index.LanguageCode);
            Assert.AreEqual(input.LanguageName, index.LanguageName);
            Assert.AreEqual(input.LanguageTextDirection, index.TextDirection);
            Assert.AreEqual(input.RepoUrl, index.RepoUrl);
            Assert.AreEqual(input.Title, index.ResourceTitle);
            Assert.AreEqual("tn", index.ResourceType);
        }
}