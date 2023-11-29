using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using BTTWriterLib.Models;
using DotLiquid;
using NUnit.Framework;
using ScriptureRenderingPipeline.Models;
using ScriptureRenderingPipeline.Renderers;
using SRPTests.TestHelpers;

namespace SRPTests;

public class BibleRendererTests
{
    private const string TestUSFM = 
@"\id GEN
\toc3 GEN
\c 1
\p
\v 1 In the beginning God created the heavens and the earth.";
    private const string TestUSFMWithInvalidTOC = 
@"\id GEN
\toc3 lfkasjdflakjdlfj
\c 1
\p
\v 1 In the beginning God created the heavens and the earth.";
    private const string TestUSFMWithMissingTOC = 
@"\id GEN
\c 1
\p
\v 1 In the beginning God created the heavens and the earth.";
    private const string TestUSFMWithDuplicateChapters = 
@"\id GEN
\c 1
\p
\v 1 In the beginning God created the heavens and the earth.\c 1";

    private const string BTTWriterText = 
@"\v 1 In the beginning God created the heavens and the earth.";
    private string ResultHtml = "<div id=\"ch-1\" class=\"chapter\">\n" +
                                  "<span class=\"chaptermarker\">1</span>\n" +
                                  "<p>\n" +
                                  "<span class=\"verse\">\n" +
                                  "<sup class=\"versemarker\">1</sup>\n" +
                                  "In the beginning God created the heavens and the earth.\n" +
                                  "</span>\n" +
                                  "</p>\n" +
                                  "</div>\n\n\n".SanitizeNewlines();
private string BTTWriterOutput = "<div id=\"ch-1\" class=\"chapter\">\n" +
"<span class=\"chaptermarker\">1</span>\n" +
"<span class=\"verse\">\n" +
"<sup class=\"versemarker\">1</sup>\n" +
"In the beginning God created the heavens and the earth.\n" +
"</span>\n" +
"</div>\n\n\n".SanitizeNewlines();
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
            LanguageCode = "en",
            LanguageName = "English",
            LanguageTextDirection = "ltr",
            ResourceName = "ulb",
            Title = "English ULB",
            RepoUrl = "https://content.bibletranslationtools.org/u/username/repo",
            PrintTemplate = Template.Parse("{{ content }}"),
        };
        await renderer.RenderAsync(rendererInput, fakeOutputInterface);
        Assert.AreEqual(5, fakeOutputInterface.Files.Count);
        Assert.AreEqual(ResultHtml.SanitizeNewlines() + Environment.NewLine, fakeOutputInterface.Files["print_all.html"].SanitizeNewlines());
        Assert.AreEqual(ResultHtml.SanitizeNewlines(), fakeOutputInterface.Files["GEN/1.html"].SanitizeNewlines());
        var downloadIndex = JsonSerializer.Deserialize<DownloadIndex>(fakeOutputInterface.Files["download.json"]);
        var index = JsonSerializer.Deserialize<OutputIndex>(fakeOutputInterface.Files["index.json"]);
        var genWhole = JsonSerializer.Deserialize<OutputIndex>(fakeOutputInterface.Files["GEN/whole.json"]);
        
        Assert.AreEqual(rendererInput.LanguageCode, index.LanguageCode);
        Assert.AreEqual(rendererInput.LanguageName, index.LanguageName);
        Assert.AreEqual(rendererInput.LanguageTextDirection, index.TextDirection);
        Assert.AreEqual("bible", index.ResourceType);
        Assert.AreEqual(rendererInput.Title, index.ResourceTitle);
        Assert.AreEqual(rendererInput.RepoUrl, index.RepoUrl);
        Assert.IsTrue(DateTime.Now - DateTime.Parse(index.LastRendered) < TimeSpan.FromSeconds(30));
        
        Assert.AreEqual(1, index.Bible.Count);
        Assert.AreEqual("GEN", index.Bible[0].Slug);
        Assert.AreEqual(1, index.Bible[0].Chapters.Count);
        Assert.AreEqual(1, index.Bible[0].Chapters[0].VerseCount);
        
        Assert.IsTrue(DateTime.Now - DateTime.Parse(downloadIndex.LastRendered) < TimeSpan.FromSeconds(30));
        Assert.AreEqual("GEN", downloadIndex.Content[0].Slug);
        Assert.AreEqual(ResultHtml.SanitizeNewlines().Length, downloadIndex.Content[0].Chapters[0].ByteCount);
        Assert.AreEqual(ResultHtml.SanitizeNewlines(), downloadIndex.Content[0].Chapters[0].Content);
        Assert.AreEqual("1", downloadIndex.Content[0].Chapters[0].Label);
        Assert.AreEqual("1", downloadIndex.Content[0].Chapters[0].Number);
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
         Assert.AreEqual(fakeOutputInterface.Files["GEN/1.html"].SanitizeNewlines(), ResultHtml.SanitizeNewlines());       
         Assert.AreEqual(fakeOutputInterface.Files["EXO/1.html"].SanitizeNewlines(), ResultHtml.SanitizeNewlines());       
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
         Assert.AreEqual(fakeOutputInterface.Files["GEN/1.html"].SanitizeNewlines(), ResultHtml.SanitizeNewlines());       
    }
    [Test]
    public async Task TestWithMultipleChapters()
    {
         var fakeFileSystem = new FakeZipFileSystem();
         fakeFileSystem.AddFolder("base");
         fakeFileSystem.AddFile("base/01-GEN.usfm", TestUSFMWithDuplicateChapters);
         
         var fakeOutputInterface = new FakeOutputInterface();
         var renderer = new BibleRenderer();
         var rendererInput = new RendererInput()
         {
             FileSystem = fakeFileSystem,
             PrintTemplate = Template.Parse("{{ content }}"),
         };
         await renderer.RenderAsync(rendererInput, fakeOutputInterface);
         Assert.AreEqual(5, fakeOutputInterface.Files.Count);
         Assert.AreEqual(fakeOutputInterface.Files["GEN/1.html"].SanitizeNewlines(), ResultHtml.SanitizeNewlines());       
    }

    [Test]
    public async Task TestWithBTTWriterProjects()
    {
        var fakeFileSystem = new FakeZipFileSystem();
        fakeFileSystem.AddFolder("01");
        fakeFileSystem.AddFile("/01/01.txt", BTTWriterText);
        var writerManifest = new BTTWriterManifest()
        {
            project = new IdNameCombo()
            {
                name = "Genesis",
                id = "GEN"
            }
        };
        fakeFileSystem.AddFile("/manifest.json", JsonSerializer.Serialize(writerManifest));
        
        var fakeOutputInterface = new FakeOutputInterface();
        var renderer = new BibleRenderer();
        var rendererInput = new RendererInput()
        {
            FileSystem = fakeFileSystem,
            LanguageCode = "en",
            LanguageName = "English",
            LanguageTextDirection = "ltr",
            ResourceName = "ulb",
            Title = "English ULB",
            RepoUrl = "https://content.bibletranslationtools.org/u/username/repo",
            PrintTemplate = Template.Parse("{{ content }}"),
            IsBTTWriterProject = true,
        };
        await renderer.RenderAsync(rendererInput, fakeOutputInterface);
        Assert.AreEqual(BTTWriterOutput.SanitizeNewlines(), fakeOutputInterface.Files["GEN/1.html"].SanitizeNewlines());
    }
}