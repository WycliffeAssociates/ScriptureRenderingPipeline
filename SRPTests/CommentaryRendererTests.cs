using System;
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
    private const string ChapterOneContent = """
                                            # Chapter 1
                                            This is the content
                                            """;
    
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
        inputFileSystem.AddFolder("01-gen");
        inputFileSystem.AddFile("base/01-gen/01.md", ChapterOneContent);
        
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
            ResourceContainer = resourceContainer
        };
        var renderer = new CommentaryRenderer();
        await renderer.RenderAsync(input, outputFileSystem);
        Assert.AreEqual(4, outputFileSystem.Files.Count);
    }
}