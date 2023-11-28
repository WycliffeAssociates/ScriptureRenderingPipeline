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
    private const string FileContent01 = @"This is the content
[second section](../second/01.md)
[third section](../../other-section/third/01.md)
[fourth link](../something/fourth/01.md)
[blank link]()
[other link](https://content.bibletranslationtools.org/WA-Catalog/en_tm/01.md)";
    private const string SubTitle = "Subtitle";
    private const string TitleContent = "Title";

    private string ExpectedOutput = @"<h1>Intro</h1>
<div id=""ta-intro""></div>
<h2>Title</h2>
<div>This section answers the following question: Subtitle</div>
<br/>
<p>This is the content
<a href=""intro.html#second"">second section</a>
<a href=""other-section.html#third"">third section</a>
<a href=""../something/fourth/01.md"">fourth link</a>
<a href="""">blank link</a>
<a href=""https://content.bibletranslationtools.org/WA-Catalog/en_tm/01.md"">other link</a></p>

<hr/>
".SanitizeNewlines();
    
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
                    title = "Nested",
                    path = "nested",
                    sort = 2
                },
                new Project()
                {
                    title = "Missing",
                    path = "missing",
                    sort = 3
                },
                new Project()
                {
                    title = "Missing TOC",
                    path = "missing-toc",
                    sort = 4
                },
                new Project()
                {
                    title = "Invalid TOC",
                    path = "invalid-toc",
                    sort = 5
                }
            }
        };
        
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
        var nestedTableOfContents = new TableOfContents()
        {
            title = "Introduction to Translation Manual",
            sections = new()
            {
                new TableOfContents()
                {
                    title = "Nested",
                    link = "nested",
                    sections = new()
                    {
                        new TableOfContents()
                        {
                            title = "Really Nested",
                            link = "really-nested"
                        }
                    }
                },
                new TableOfContents()
                {
                    title = "Return to normal",
                    link = "return-to-normal"
                }
            }
        };

        var missingTableOfContents = new TableOfContents()
        {
            title = "Missing",
            sections = new()
            {
                new TableOfContents()
                {
                    title = "Missing",
                    link = "missing"
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
        
        // Nested navigation testing
        inputFileSystem.AddFolder("base/nested/nested");
        inputFileSystem.AddFolder("base/nested/really-nested");
        inputFileSystem.AddFolder("base/nested/return-to-normal");
        inputFileSystem.AddFile("base/nested/toc.yaml", serializer.Serialize(nestedTableOfContents));
        inputFileSystem.AddFile(("base/nested/nested/01.md"), FileContent01);
        inputFileSystem.AddFile(("base/nested/really-nested/01.md"), FileContent01);
        inputFileSystem.AddFile(("base/nested/return-to-normal/01.md"), FileContent01);
        inputFileSystem.AddFile(("base/nested/nested/title.md"), TitleContent);
        inputFileSystem.AddFile(("base/nested/really-nested/title.md"), TitleContent);
        inputFileSystem.AddFile(("base/nested/return-to-normal/title.md"), TitleContent);
        
        // Missing title testing
        inputFileSystem.AddFolder("base/missing");
        inputFileSystem.AddFile("base/missing/toc.yaml", serializer.Serialize(missingTableOfContents));
        
        inputFileSystem.AddFolder("base/missing-toc");
        
        inputFileSystem.AddFolder("base/invalid-toc");
        inputFileSystem.AddFile("base/invalid-toc/toc.yaml","lkjfalkj:jj111:11" );
        
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
        Assert.AreEqual("intro.html", index.Navigation[0].File);
        
        Assert.AreEqual(3, index.Navigation.Count);
        
        Assert.AreEqual(tableOfContents.title, index.Navigation[0].Label);
        Assert.AreEqual(nestedTableOfContents.title, index.Navigation[1].Label);
        Assert.AreEqual(nestedTableOfContents.sections[0].title, index.Navigation[1].Children[0].Label);
        Assert.AreEqual(nestedTableOfContents.sections[0].sections[0].title, index.Navigation[1].Children[0].Children[0].Label);
        Assert.AreEqual(nestedTableOfContents.sections[1].title, index.Navigation[1].Children[1].Label);

        Assert.AreEqual(missingTableOfContents.title, index.Navigation[2].Label);
        
        var lookup = JsonSerializer.Deserialize<Dictionary<string,string>>(outputFileSystem.Files["intro.json"]);
        Assert.IsTrue(lookup.ContainsKey("ta-intro"));
        Assert.AreEqual("Title", lookup["ta-intro"]);
    }
}