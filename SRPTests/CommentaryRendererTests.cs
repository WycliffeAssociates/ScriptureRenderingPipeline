using System;
using System.Text.Json;
using System.Threading.Tasks;
using DotLiquid;
using NUnit.Framework;
using PipelineCommon.Models.ResourceContainer;
using ScriptureRenderingPipeline.Models;
using ScriptureRenderingPipeline.Renderers;
using SRPTests.TestHelpers;

namespace SRPTests;

public class CommentaryRendererTests
{
    private const string ChapterOneContent = @"# Chapter 1
This is the content
[empty link]()
[image link](../images/image.png)
[01](01/second.md)
[article](../articles/article.md)";
    private string ExpectedChapterOneOutput = @"<h1 id=""chapter-1"">Chapter 1</h1>
<p>This is the content
<a href="""">empty link</a>
<a href=""../images/image.png"">image link</a>
<a href=""01/second.md"">01</a>
<a href=""popup://article.html"">article</a></p>
".SanitizeNewlines();
    private const string IntroContent = @"# Intro
This is the intro";

    private string ExpectedIntroOutput = @"<h1 id=""intro"">Intro</h1>
<p>This is the intro</p>
".SanitizeNewlines();
    private const string ArticleContent = @"# Article
This is the article";
    private string ExpectedArticleOutput = @"<h1 id=""article"">Article</h1>
<p>This is the article</p>
".SanitizeNewlines();
    
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
        var renderer = new CommentaryRenderer();
        await renderer.RenderAsync(input, outputFileSystem);
        Assert.AreEqual(3, outputFileSystem.Files.Count);
    }
    [Test]
    public async Task TestWithContent()
    {
        var outputFileSystem = new FakeOutputInterface();
        var inputFileSystem = new FakeZipFileSystem();
        inputFileSystem.AddFolder("base");
        inputFileSystem.AddFolder("base/01-gen");
        inputFileSystem.AddFolder("base/articles");
        inputFileSystem.AddFile("base/01-gen/01.md", ChapterOneContent);
        inputFileSystem.AddFile("base/01-gen/intro.md", IntroContent);
        inputFileSystem.AddFile("base/01-gen/junk.md", "Total junk here");
        inputFileSystem.AddFile("base/articles/article.md", ArticleContent);
        inputFileSystem.AddFile("base/articles/second.md", ArticleContent);
        
        var resourceContainer = new ResourceContainer()
        {
            projects = new []
            {
                new Project()
                {
                    title = "Title",
                    identifier = "gen",
                    path = "01-gen",
                    sort = 1
                }
            }
        };
        
        var input = new RendererInput()
        {
            FileSystem = inputFileSystem,
            PrintTemplate = Template.Parse("{{ content }}"),
            ResourceContainer = resourceContainer,
            BasePath = "base",
            Title = "English Commentary",
            LanguageCode = "en",
            LanguageName = "English",
            RepoUrl = "https://content.bibletranslationtools.org",
            LanguageTextDirection = "ltr",
        };
        var renderer = new CommentaryRenderer();
        await renderer.RenderAsync(input, outputFileSystem);
        
        var index = JsonSerializer.Deserialize<OutputIndex>(outputFileSystem.Files["index.json"]);
        Assert.AreEqual("ltr", index.TextDirection);
        Assert.AreEqual(input.LanguageCode, index.LanguageCode);
        Assert.AreEqual(input.LanguageName, index.LanguageName);
        Assert.AreEqual("commentary", index.ResourceType);
        Assert.AreEqual(input.Title, index.ResourceTitle);
        Assert.AreEqual(input.RepoUrl, index.RepoUrl);
        Assert.AreEqual(1, index.Bible.Count);
        
        Assert.AreEqual(ExpectedIntroOutput, outputFileSystem.Files["gen/intro.html"]);
        Assert.AreEqual(ExpectedChapterOneOutput, outputFileSystem.Files["gen/01.html"]);
        Assert.AreEqual(ExpectedArticleOutput, outputFileSystem.Files["article.html"]);
        Assert.AreEqual(ExpectedArticleOutput, outputFileSystem.Files["second.html"]);
    }
}