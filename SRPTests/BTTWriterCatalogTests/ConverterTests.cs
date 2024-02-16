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
}