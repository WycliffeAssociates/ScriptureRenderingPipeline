using System.Collections.Generic;
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
             ResourceContainer = resourceContainer,
             PrintTemplate = Template.Parse("{{ content }}")
         };
         var renderer = new TranslationWordsRenderer();
         await renderer.RenderAsync(input, outputFileSystem);       
         Assert.AreEqual(ExpectedResult, outputFileSystem.Files["kt.html"]);
    }
}