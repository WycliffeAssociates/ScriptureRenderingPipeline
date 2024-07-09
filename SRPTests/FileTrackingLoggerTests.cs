using System;
using System.Security.Cryptography;
using NUnit.Framework;
using PipelineCommon.Helpers;
using ScriptureRenderingPipelineWorker;

namespace SRPTests;

public class FileTrackingLoggerTests
{
    [Test]
    public void Test()
    {
        var logger = new FileTrackingLogger("https://read.bibletranslationtools.org/", RepoType.Unknown);
        const string warningText = "Warning";
        const string errorText = "Error";
        const string content = "This is some content";
        logger.LogWarning(warningText);
        logger.LogError(errorText);
        logger.LogFile("test.txt", content);
        Assert.AreEqual(1, logger.Warnings.Count);
        Assert.AreEqual(warningText, logger.Warnings[0]);
        Assert.AreEqual(1, logger.Errors.Count);
        Assert.AreEqual(errorText, logger.Errors[0]);
        Assert.AreEqual(1, logger.Files.Count);
        Assert.AreEqual("/test.txt", logger.Files[0].Path);
        Assert.AreEqual(content.Length, logger.Files[0].Size);
        Assert.AreEqual("txt", logger.Files[0].FileType);
        // Check that the hash for the content matches what we expect
        Assert.AreEqual(Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content))), logger.Files[0].Hash);
    }

    [Test]
    public void TestBookChapterLogging()
    {
        var logger = new FileTrackingLogger("https://read.bibletranslationtools.org/", RepoType.Bible);
        logger.LogFile("gen/1.html", "This is some content");
        Assert.AreEqual("gen", logger.Files[0].Book);
        Assert.AreEqual(1, logger.Files[0].Chapter);
        logger.LogFile("gen/something.html", "This is some content");
        Assert.AreEqual("gen", logger.Files[1].Book);
        Assert.AreEqual(null, logger.Files[1].Chapter);
    }

    [Test]
    public void TestSlugExtract()
    {
        var logger = new FileTrackingLogger("https://read.bibletranslationtools.org/", RepoType.translationWords);
        logger.LogFile("terms.html", "This is some content");
        Assert.AreEqual("terms", logger.Files[0].Slug);
        logger.LogFile("print_all.html", "This is some content");
        Assert.AreEqual(null, logger.Files[1].Slug);
    }
}