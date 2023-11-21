using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DotLiquid;
using NUnit.Framework;
using ScriptureRenderingPipeline.Models;
using ScriptureRenderingPipeline.Renderers;
using SRPTests.TestHelpers;

namespace SRPTests;

public class BibleRendererTests
{
    private const string TestUSFM = 
"""
\id GEN
\toc3 GEN
\c 1
\p
\v 1 In the beginning God created the heavens and the earth.
""";
    private const string TestUSFMWithInvalidTOC = 
"""
\id GEN
\toc3 lfkasjdflakjdlfj
\c 1
\p
\v 1 In the beginning God created the heavens and the earth.
""";
    private const string TestUSFMWithMissingTOC = 
"""
\id GEN
\c 1
\p
\v 1 In the beginning God created the heavens and the earth.
""";
    private const string ResultHtml = "<div id=\"ch-1\" class=\"chapter\">\n" +
                                  "<span class=\"chaptermarker\">1</span>\n" +
                                  "<p>\n" +
                                  "<span class=\"verse\">\n" +
                                  "<sup class=\"versemarker\">1</sup>\n" +
                                  "In the beginning God created the heavens and the earth.\n" +
                                  "</span>\n" +
                                  "</p>\n" +
                                  "</div>\n\n\n";
    [Test]
    public async Task TestWithNothing()
    {
        var fakeFileSystem = new FakeZipFileSystem();
        var fakeOutputInterface = new FakeOutputInterface();
        var renderer = new BibleRenderer();
        var rendererInput = new RendererInput()
        {
            FileSystem = fakeFileSystem
        };
        await renderer.RenderAsync(rendererInput, fakeOutputInterface);
        Assert.AreEqual(2, fakeOutputInterface.Files.Count);
    }
    [Test]
    public async Task TestWithFiles()
    {
        var fakeFileSystem = new FakeZipFileSystem();
        fakeFileSystem.AddFolder("base");
        fakeFileSystem.AddFile("base/GEN.usfm", TestUSFM);
        
        var fakeOutputInterface = new FakeOutputInterface();
        var renderer = new BibleRenderer();
        var rendererInput = new RendererInput()
        {
            FileSystem = fakeFileSystem,
            PrintTemplate = Template.Parse("{{ content }}"),
        };
        await renderer.RenderAsync(rendererInput, fakeOutputInterface);
        Assert.AreEqual(5, fakeOutputInterface.Files.Count);
        Assert.AreEqual(fakeOutputInterface.Files["print_all.html"], ResultHtml + "\n");
        Assert.AreEqual(fakeOutputInterface.Files["GEN/1.html"], ResultHtml);
    }

    [Test]
    public async Task TestWithMissingTOC()
    {
         var fakeFileSystem = new FakeZipFileSystem();
         fakeFileSystem.AddFolder("base");
         fakeFileSystem.AddFile("base/01-GEN.usfm", TestUSFMWithMissingTOC);
         fakeFileSystem.AddFile("base/Exo.usfm", TestUSFMWithMissingTOC);
         
         var fakeOutputInterface = new FakeOutputInterface();
         var renderer = new BibleRenderer();
         var rendererInput = new RendererInput()
         {
             FileSystem = fakeFileSystem,
             PrintTemplate = Template.Parse("{{ content }}"),
         };
         await renderer.RenderAsync(rendererInput, fakeOutputInterface);
         Assert.AreEqual(fakeOutputInterface.Files["GEN/1.html"], ResultHtml);       
         Assert.AreEqual(fakeOutputInterface.Files["EXO/1.html"], ResultHtml);       
    }
    [Test]
    public async Task TestWithInvalidTOC()
    {
         var fakeFileSystem = new FakeZipFileSystem();
         fakeFileSystem.AddFolder("base");
         fakeFileSystem.AddFile("base/01-GEN.usfm", TestUSFMWithInvalidTOC);
         
         var fakeOutputInterface = new FakeOutputInterface();
         var renderer = new BibleRenderer();
         var rendererInput = new RendererInput()
         {
             FileSystem = fakeFileSystem,
             PrintTemplate = Template.Parse("{{ content }}"),
         };
         await renderer.RenderAsync(rendererInput, fakeOutputInterface);
         Assert.AreEqual(5, fakeOutputInterface.Files.Count);
         Assert.AreEqual(fakeOutputInterface.Files["GEN/1.html"], ResultHtml);       
    }
}