using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using DotLiquid;
using NUnit.Framework;
using PipelineCommon.Models.ResourceContainer;
using ScriptureRenderingPipeline.Models;
using ScriptureRenderingPipeline.Renderers;
using SRPTests.TestHelpers;

namespace SRPTests;

public class TranslationWordsRendererTests
{
    private const string InputMarkdown ="""
            # Adam
            This is adam
            [Eve](./eve.md)
            [Cain](cain.md)
            [Sacrifice](../other/sacrifice.md)
            [Random Link](https://example.com)
            [Random MD Link](../../../test.md)
            """;
    private const string ExpectedResult = """
            <h1>Key Terms</h1>
            <div id="adam"></div>
            <h2 id="adam">Adam</h2>
            <p>This is adam
            <a href="kt.html#eve">Eve</a>
            <a href="kt.html#cain">Cain</a>
            <a href="other.html#sacrifice">Sacrifice</a>
            <a href="https://example.com">Random Link</a>
            <a href="../../../test.md">Random MD Link</a></p>

            <hr/>

            """;

    [Test]
    public async Task TestEmpty()
    {
        var resourceContainer = new ResourceContainer()
        {
            projects = new[]
            {
                new Project()
                {
                    path = "words"
                }
            }

        };
        
        var inputFileSystem = new FakeZipFileSystem();
        
        var outputFileSystem = new FakeOutputInterface();
        var input = new RendererInput()
        {
            FileSystem = inputFileSystem,
            ResourceContainer = resourceContainer,
        };
        var renderer = new TranslationWordsRenderer();
        await renderer.RenderAsync(input, outputFileSystem);
    }

    [Test]
    public async Task TestWithContent()
    {
         var resourceContainer = new ResourceContainer()
         {
             projects = new[]
             {
                 new Project()
                 {
                     path = "words"
                 }
             }
 
         };
         
         var inputFileSystem = new FakeZipFileSystem();
         
         inputFileSystem.AddFolder("/words");
         inputFileSystem.AddFolder("/words/kt");
         inputFileSystem.AddFile("/words/kt/adam.md", InputMarkdown);
         
         var outputFileSystem = new FakeOutputInterface();
         var input = new RendererInput()
         {
             FileSystem = inputFileSystem,
             LanguageCode = "es",
             LanguageName = "Spanish",
             Title = "Spanish Words",
             RepoUrl = "https://git.door43.org/Door43-Catalog/kt",
             ResourceContainer = resourceContainer,
             LanguageTextDirection = "ltr",
             AppsMeta = new() {FontFamily = "ff", FontUrl = "https://example.com/font"},
             PrintTemplate = Template.Parse("{{ content }}")
         };
         var renderer = new TranslationWordsRenderer();
         await renderer.RenderAsync(input, outputFileSystem);       
         Assert.AreEqual(ExpectedResult, outputFileSystem.Files["kt.html"]);
         var keywordsLookup = JsonSerializer.Deserialize<Dictionary<string,string>>(outputFileSystem.Files["kt.json"]);
         var index = JsonSerializer.Deserialize<OutputIndex>(outputFileSystem.Files["index.json"]);
         
         Assert.IsTrue(keywordsLookup.ContainsKey("adam"));
         Assert.AreEqual("Adam", keywordsLookup["adam"]);
         
         Assert.AreEqual(input.LanguageCode, index.LanguageCode);
         Assert.AreEqual(input.LanguageName, index.LanguageName);
         Assert.AreEqual(input.RepoUrl, index.RepoUrl);
         Assert.AreEqual("tw", index.ResourceType);
         Assert.AreEqual(input.LanguageTextDirection, index.TextDirection);
         
         Assert.AreEqual(input.AppsMeta.FontFamily, index.AppMeta.FontFamily);
         Assert.AreEqual(input.AppsMeta.FontUrl, index.AppMeta.FontUrl);
         
         Assert.AreEqual(1, index.Words.Count);
         Assert.AreEqual(1, index.Words[0].Words.Count);
         Assert.AreEqual("adam", index.Words[0].Words[0].Slug);
         Assert.AreEqual("Adam", index.Words[0].Words[0].Label);
         Assert.AreEqual("Key Terms", index.Words[0].Label);
         Assert.AreEqual("kt", index.Words[0].Slug);
         
    }
}