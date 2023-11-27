using System.Text.Json;
using System.Threading.Tasks;
using DotLiquid;
using NUnit.Framework;
using ScriptureRenderingPipeline.Models;
using ScriptureRenderingPipeline.Renderers;
using SRPTests.TestHelpers;

namespace SRPTests;

public class TranslationQuestionsRendererTests
{
    private const string InputMarkdown = """
                                         # What is the answer to the life, the universe, and everything?
                                         
                                         42
                                         """;
    private const string FrontMatterMarkdown = """
                                               # Intro to Genesis
                                               """;

    private const string FrontMatterExpected = """
                                               <div id="tq-chapter-gen-front"></div>
                                               <div id="tq-chapter-gen-front-intro"></div>
                                               <h2 id="intro-to-genesis">Intro to Genesis</h2>
                                               
                                               
                                               """;
    private const string ExpectedResult = """
                                          <h1 id="tq-chapter-gen-01">Genesis 1</h2>
                                          <h1 id="tq-chapter-gen-01-01">Genesis 1:1</h2>
                                          <h2 id="what-is-the-answer-to-the-life-the-universe-and-everything">What is the answer to the life, the universe, and everything?</h2>
                                          <p>42</p>
                                          
                                          
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
        var renderer = new TranslationQuestionsRenderer();
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
            Title = "French Translation Questions"
        };
        var renderer = new TranslationQuestionsRenderer();
        await renderer.RenderAsync(input, outputFileSystem);
        Assert.AreEqual(ExpectedResult, outputFileSystem.Files["gen/01.html"]);
        Assert.AreEqual(FrontMatterExpected, outputFileSystem.Files["gen/front.html"]);
        
        var index = JsonSerializer.Deserialize<OutputIndex>(outputFileSystem.Files["index.json"]);
        
        Assert.AreEqual(input.LanguageCode, index.LanguageCode);
        Assert.AreEqual(input.LanguageName, index.LanguageName);
        Assert.AreEqual(input.LanguageTextDirection, index.TextDirection);
        Assert.AreEqual(input.RepoUrl, index.RepoUrl);
        Assert.AreEqual(input.Title, index.ResourceTitle);
        Assert.AreEqual("tq", index.ResourceType);
    }
}