using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using DotLiquid;
using NUnit.Framework;
using PipelineCommon.Models.ResourceContainer;
using ScriptureRenderingPipeline.Models;
using ScriptureRenderingPipeline.Renderers;
using SRPTests.TestHelpers;
using YamlDotNet.Serialization;

namespace SRPTests;

public class TranslationManualRendererTests
{
    private ISerializer serializer;
    private const string FileContent01 = """
                                         This is the content
                                         [second section](../second/01.md)
                                         [third section](../../other-section/third/01.md)
                                         [fourth link](../something/fourth/01.md)
                                         [other link](https://content.bibletranslationtools.org/WA-Catalog/en_tm/01.md)
                                         """;
    private const string SubTitle = "Subtitle";
    private const string TitleContent = "Title";

    private const string ExpectedOutput = """
                                          <h1>Intro</h1>
                                          <div id="ta-intro"></div>
                                          <h2>Title</h2>
                                          <div>This section answers the following question: Subtitle</div>
                                          <br/>
                                          <p>This is the content
                                          <a href="intro.html#second">second section</a>
                                          <a href="other-section.html#third">third section</a>
                                          <a href="../something/fourth/01.md">fourth link</a>
                                          <a href="https://content.bibletranslationtools.org/WA-Catalog/en_tm/01.md">other link</a></p>

                                          <hr/>
                                          
                                          """;
    
    [SetUp]
    public void Setup()
    {
        serializer = new SerializerBuilder().Build();
    }
    [Test]
    public async Task TestWithNothing()
    {            
        var outputFileSystem = new FakeOutputInterface();
        var inputFileSystem = new FakeZipFileSystem();
        var resourceContainer = new ResourceContainer()
        {
            projects = Array.Empty<Project>()
        };
        var input = new RendererInput()
        {
            FileSystem = inputFileSystem,
            PrintTemplate = Template.Parse("{{ content }}"),
            ResourceContainer = resourceContainer
        };
        var renderer = new TranslationManualRenderer();
        await renderer.RenderAsync(input, outputFileSystem);
        Assert.AreEqual(1, outputFileSystem.Files.Count);
    }
    [Test]
    public async Task TestWithContent()
    {            
        var outputFileSystem = new FakeOutputInterface();
        var inputFileSystem = new FakeZipFileSystem();
        var tableOfContents = new TableOfContents()
        {
            title = "Introduction to Translation Manual",
            sections = new()
            {
                new TableOfContents()
                {
                    title = "Translation Manual Sections",
                    link = "ta-intro"
                }
            }
        };
        inputFileSystem.AddFolder("base");
        inputFileSystem.AddFolder("base/intro");
        inputFileSystem.AddFile("base/intro/toc.yaml", serializer.Serialize(tableOfContents));
        inputFileSystem.AddFolder("base/intro/ta-intro");
        inputFileSystem.AddFolder("base/intro/second");
        inputFileSystem.AddFile(("base/intro/ta-intro/01.md"), FileContent01);
        inputFileSystem.AddFile(("base/intro/ta-intro/title.md"), TitleContent);
        inputFileSystem.AddFile(("base/intro/ta-intro/sub-title.md"), SubTitle);
        var resourceContainer = new ResourceContainer()
        {
            projects = new[]
            {
                new Project()
                {
                    title = "Intro",
                    path = "intro",
                    sort = 1
                },
                new Project()
                {
                    title = "Second",
                    path = "second",
                    sort = 2
                }
            }
        };
        var input = new RendererInput()
        {
            FileSystem = inputFileSystem,
            PrintTemplate = Template.Parse("{{ content }}"),
            ResourceContainer = resourceContainer,
            BasePath = "base",
            LanguageCode = "en",
            Title = "English Translation Manual",
            LanguageTextDirection = "ltr",
        };
        var renderer = new TranslationManualRenderer();
        await renderer.RenderAsync(input, outputFileSystem);
        
        Assert.AreEqual(ExpectedOutput, outputFileSystem.Files["intro.html"]);
        
        var index = JsonSerializer.Deserialize<OutputIndex>(outputFileSystem.Files["index.json"]);
        Assert.AreEqual("tm", index.ResourceType);
        Assert.AreEqual(input.LanguageCode, index.LanguageCode);
        Assert.AreEqual(input.LanguageTextDirection, index.TextDirection);
        Assert.AreEqual(input.Title, index.ResourceTitle);
        Assert.AreEqual(input.LanguageName, index.LanguageName);
        
        var lookup = JsonSerializer.Deserialize<Dictionary<string,string>>(outputFileSystem.Files["intro.json"]);
        Assert.IsTrue(lookup.ContainsKey("ta-intro"));
        Assert.AreEqual("Title", lookup["ta-intro"]);
    }
}