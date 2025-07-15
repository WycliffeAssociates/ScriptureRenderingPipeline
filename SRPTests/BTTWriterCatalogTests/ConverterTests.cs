using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BTTWriterCatalog.ContentConverters;
using BTTWriterCatalog.Models;
using BTTWriterCatalog.Models.OutputFormats;
using NUnit.Framework;
using PipelineCommon.Models.ResourceContainer;
using SRPTests.TestHelpers;

namespace SRPTests.BTTWriterCatalogTests;

public class ConverterTests
{
    [Test]
    public async Task TestEmpty()
    {
        var basePath = "path";
        var fileSystem = new FakeZipFileSystem();
        var outputInterface = new FakeOutputInterface();
        var chunks = new Dictionary<string, Dictionary<int, List<VerseChunk>>>();
        var resourceContainer = new ResourceContainer()
        {
            projects = Array.Empty<Project>()
        };

        await Scripture.ConvertAsync(fileSystem, basePath, outputInterface, resourceContainer, chunks, new FakeLogger());
    }

    [Test]
    public async Task TestWithContent()
    {
        var basePath = "path";
        var usfmContent = @"\c 1 \v 1 In the beginning God created the heavens and the earth.";
        var fileSystem = new FakeZipFileSystem();
        fileSystem.AddFile("path/gen.usfm", usfmContent);
        var outputInterface = new FakeOutputInterface();
        var chunks = new Dictionary<string, Dictionary<int, List<VerseChunk>>>()
        {
            ["GEN"] = new()
            {
                [1] = new() { new VerseChunk(1, 1) }
            }
        };
        var resourceContainer = new ResourceContainer()
        {
            projects = new [] { new Project() { identifier = "GEN", path = "gen.usfm" } }
        };

        await Scripture.ConvertAsync(fileSystem, basePath, outputInterface, resourceContainer, chunks, new FakeLogger());
        Assert.AreEqual(2, outputInterface.Files.Count);
        Assert.AreEqual(outputInterface.Files["gen/gen.usfm"], usfmContent);
        
        var outputChunks = JsonSerializer.Deserialize<ScriptureResource>(outputInterface.Files["gen/source.json"]);
        Assert.AreEqual(1,outputChunks.Chapters.Count);
        Assert.AreEqual("1-1", outputChunks.Chapters[0].Frames[0].Id);
        Assert.AreEqual("1", outputChunks.Chapters[0].Frames[0].LastVerse);
        Assert.AreEqual($"<verse number=\"1\" style=\"v\" />{Environment.NewLine}In the beginning God created the heavens and the earth.", outputChunks.Chapters[0].Frames[0].Text);
    }

    [Test]
    public async Task TestWithMissingChapters()
    {
        var basePath = "path";
        var usfmContent = @"\c 1 \v 1 In the beginning God created the heavens and the earth.";
        var fileSystem = new FakeZipFileSystem();
        fileSystem.AddFile("path/gen.usfm", usfmContent);
        var outputInterface = new FakeOutputInterface();
        var logger = new FakeLogger();
        var chunks = new Dictionary<string, Dictionary<int, List<VerseChunk>>>()
        {
            ["GEN"] = new()
            {
                [1] = new() { new VerseChunk(1, 1) },
                [2] = new() { new VerseChunk(1, 1) }
            }
        };
        var resourceContainer = new ResourceContainer()
        {
            projects = new [] { new Project() { identifier = "GEN", path = "gen.usfm" } }
        };
        
        await Scripture.ConvertAsync(fileSystem, basePath, outputInterface, resourceContainer, chunks, logger);
        Assert.AreEqual("No verses found for GEN 2", logger.ErrorMessages[0]);
    }
    
    [Test]
    public async Task TestWithWordMarkers()
    {
        var basePath = "path";
        var usfmContent = @"\c 1 \v 1 In the beginning \w God \w* created the heavens and the earth.";
        var fileSystem = new FakeZipFileSystem();
        fileSystem.AddFile("path/gen.usfm", usfmContent);
        var outputInterface = new FakeOutputInterface();
        var chunks = new Dictionary<string, Dictionary<int, List<VerseChunk>>>()
        {
            ["GEN"] = new()
            {
                [1] = new() { new VerseChunk(1, 1) }
            }
        };
        var resourceContainer = new ResourceContainer()
        {
            projects = new [] { new Project() { identifier = "GEN", path = "gen.usfm" } }
        };

        await Scripture.ConvertAsync(fileSystem, basePath, outputInterface, resourceContainer, chunks, new FakeLogger());
        Assert.AreEqual(2, outputInterface.Files.Count);
        Assert.AreEqual(outputInterface.Files["gen/gen.usfm"], usfmContent);
        
        var outputChunks = JsonSerializer.Deserialize<ScriptureResource>(outputInterface.Files["gen/source.json"]);
        Assert.AreEqual(1,outputChunks.Chapters.Count);
        Assert.AreEqual("1-1", outputChunks.Chapters[0].Frames[0].Id);
        Assert.AreEqual("1", outputChunks.Chapters[0].Frames[0].LastVerse);
        Assert.AreEqual($"<verse number=\"1\" style=\"v\" />{Environment.NewLine}In the beginning God created the heavens and the earth.", outputChunks.Chapters[0].Frames[0].Text);
    }

    [Test]
    public async Task TestChunkStartsInMiddleOfVerseBridge_AdjustsPreviousChunkAndIncludesBridge()
    {
        var basePath = "path";
        // USFM with a verse bridge: verse 3-5, a verse 2, and a verse 6 outside the bridge
        var usfmContent = "\\c 1 \\v 2 And darkness was upon the face of the deep. \\v 3-5 And the Lord said, 'Let there be light.' And there was light. And it was good. \\v 6 And the evening and the morning were the first day.";
        var fileSystem = new FakeZipFileSystem();
        fileSystem.AddFile("path/gen.usfm", usfmContent);
        var outputInterface = new FakeOutputInterface();
        // Chunks: 1-3 and 4-6, as is proper and sequential
        var chunks = new Dictionary<string, Dictionary<int, List<VerseChunk>>>()
        {
            ["GEN"] = new()
            {
                [1] = new() { new VerseChunk(1, 3), new VerseChunk(4, 6) }
            }
        };
        var resourceContainer = new ResourceContainer()
        {
            projects = new [] { new Project() { identifier = "GEN", path = "gen.usfm" } }
        };

        await Scripture.ConvertAsync(fileSystem, basePath, outputInterface, resourceContainer, chunks, new FakeLogger());
        var outputChunks = JsonSerializer.Deserialize<ScriptureResource>(outputInterface.Files["gen/source.json"]);
        // There should be two frames: one for 1-3 (including the bridge), one for 4-6
        Assert.AreEqual(2, outputChunks.Chapters[0].Frames.Count, "There should be two frames: one for 1-3, one for 4-6, not duplicated.");
        Assert.AreEqual("1-1", outputChunks.Chapters[0].Frames[0].Id, "First id should be 1-1, starting at verse 1.");
        Assert.AreEqual("5", outputChunks.Chapters[0].Frames[0].LastVerse, "First frame should end at verse 5.");
        Assert.IsTrue(outputChunks.Chapters[0].Frames[0].Text.Contains("Let there be light."), "First frame should contain the bridge text.");
        Assert.IsTrue(outputChunks.Chapters[0].Frames[0].Text.Contains("And it was good."), "First frame should contain the full bridge text.");
        Assert.AreEqual("1-6", outputChunks.Chapters[0].Frames[1].Id, "Second frame should be just verse 6");
        Assert.AreEqual("6", outputChunks.Chapters[0].Frames[1].LastVerse, "Second frame should end at verse 6.");
        Assert.IsTrue(outputChunks.Chapters[0].Frames[1].Text.Contains("the evening and the morning were the first day"), "Second frame should contain verse 6 text.");
    }

    [Test]
    public async Task TestChunkBoundariesAlignedWithVerseBridges()
    {
        var basePath = "path";
        var usfmContent = "\\c 1 \\v 1 In the beginning God created the heavens and the earth. \\v 2 And the earth was without form, and void; \\v 3 And darkness was upon the face of the deep. \\v 4-5 And the Spirit of God moved upon the face of the waters. And God said, Let there be light: and there was light. \\v 6 And God saw the light, that it was good: and God divided the light from the darkness.";
        var fileSystem = new FakeZipFileSystem();
        fileSystem.AddFile("path/gen.usfm", usfmContent);
        var outputInterface = new FakeOutputInterface();
        var chunks = new Dictionary<string, Dictionary<int, List<VerseChunk>>>()
        {
            ["GEN"] = new()
            {
                [1] = new() { new VerseChunk(1, 3), new VerseChunk(4, 6) }
            }
        };
        var resourceContainer = new ResourceContainer()
        {
            projects = new [] { new Project() { identifier = "GEN", path = "gen.usfm" } }
        };

        await Scripture.ConvertAsync(fileSystem, basePath, outputInterface, resourceContainer, chunks, new FakeLogger());
        var outputChunks = JsonSerializer.Deserialize<ScriptureResource>(outputInterface.Files["gen/source.json"]);
        // There should be two frames: one for 1-3, one for 4-6, perfectly aligned with chunk boundaries
        Assert.AreEqual(2, outputChunks.Chapters[0].Frames.Count, "There should be two frames: one for 1-3, one for 4-6, aligned with chunk boundaries.");
        Assert.AreEqual("1-1", outputChunks.Chapters[0].Frames[0].Id, "First id should be 1-1, starting at verse 1.");
        Assert.AreEqual("3", outputChunks.Chapters[0].Frames[0].LastVerse, "First frame should end at verse 3.");
        Assert.IsTrue(outputChunks.Chapters[0].Frames[0].Text.Contains("In the beginning God created the heavens and the earth."), "First frame should contain verse 1 text.");
        Assert.IsTrue(outputChunks.Chapters[0].Frames[0].Text.Contains("And darkness was upon the face of the deep."), "First frame should contain verse 3 text.");
        Assert.AreEqual("1-4", outputChunks.Chapters[0].Frames[1].Id, "Second frame should be 4-6");
        Assert.AreEqual("6", outputChunks.Chapters[0].Frames[1].LastVerse, "Second frame should end at verse 6.");
        Assert.IsTrue(outputChunks.Chapters[0].Frames[1].Text.Contains("And the Spirit of God moved upon the face of the waters."), "Second frame should contain verse 4-5 bridge text.");
        Assert.IsTrue(outputChunks.Chapters[0].Frames[1].Text.Contains("God said, Let there be light: and there was light."), "Second frame should contain verse 4-5 bridge text.");
        Assert.IsTrue(outputChunks.Chapters[0].Frames[1].Text.Contains("God saw the light, that it was good:"), "Second frame should contain verse 6 text.");
    }

    [Test]
    public async Task ChunkStartingInBridgeWithNoUniqueVerses_ShouldNotAppearInOutput()
    {
        var basePath = "path";
        var usfmContent = "\\id GEN\n\\c 1\n\\v 1 In the beginning God created the heavens and the earth.\n\\v 2 And the earth was without form, and void; and darkness was upon the face of the deep.\n\\v 3 And the Spirit of God moved upon the face of the waters.\n\\v 2-5 God said, Let there be light: and there was light.\n\\v 6 God saw the light, that it was good:";
        var fileSystem = new FakeZipFileSystem();
        fileSystem.AddFile("path/gen.usfm", usfmContent);
        var outputInterface = new FakeOutputInterface();
        var chunks = new Dictionary<string, Dictionary<int, List<VerseChunk>>>
        {
            ["GEN"] = new Dictionary<int, List<VerseChunk>>
            {
                [1] = new List<VerseChunk>
                {
                    new VerseChunk(1, 3), // covers verses 1-3
                    new VerseChunk(4, 5),  // bridge chunk, should be extended to cover 2-5
                    new(6, 6)  // covers verse 6
                }
            }
        };
        var resourceContainer = new ResourceContainer()
        {
            projects = new [] { new Project() { identifier = "GEN", path = "gen.usfm" } }
        };
        await Scripture.ConvertAsync(fileSystem, basePath, outputInterface, resourceContainer, chunks, new FakeLogger());
        var outputChunks = JsonSerializer.Deserialize<ScriptureResource>(outputInterface.Files["gen/source.json"]);
        // There should be only two frames: one for 1-3, one for 4-5 (bridge)
        Assert.AreEqual(2, outputChunks.Chapters[0].Frames.Count, "There should be two frames: one for 1-3, skipping 4-5 because it gets absorbed, and finally one for 6.");
        Assert.AreEqual("1-1", outputChunks.Chapters[0].Frames[0].Id, "First id should be 1-1, starting at verse 1.");
        Assert.AreEqual("5", outputChunks.Chapters[0].Frames[0].LastVerse, "First frame should end at verse 5.");
        Assert.AreEqual("1-6", outputChunks.Chapters[0].Frames[1].Id, "Second id should be 1-6");
        Assert.AreEqual("6", outputChunks.Chapters[0].Frames[1].LastVerse, "Second frame should end at verse 6.");
        Assert.IsFalse(outputChunks.Chapters[0].Frames[1].Text.Contains("God said, Let there be light: and there was light."), "Second frame should not contain bridge text.");
    }
}