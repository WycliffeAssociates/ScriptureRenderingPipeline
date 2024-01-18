using System;
using System.Security.Cryptography;
using NUnit.Framework;
using ScriptureRenderingPipelineWorker;

namespace SRPTests;

public class FileTrackingLoggerTests
{
    [Test]
    public void Test()
    {
        var logger = new FileTrackingLogger("http://read.bibletranslationtools.org/");
        var warningText = "Warning";
        var errorText = "Error";
        logger.LogWarning(warningText);
        logger.LogError(errorText);
        var content = "This is some content";
        logger.LogFile("test.txt", content);
        Assert.AreEqual(1, logger.Warnings.Count);
        Assert.AreEqual(warningText, logger.Warnings[0]);
        Assert.AreEqual(1, logger.Errors.Count);
        Assert.AreEqual(errorText, logger.Errors[0]);
        Assert.AreEqual(1, logger.Files.Count);
        Assert.AreEqual("http://read.bibletranslationtools.org/test.txt", logger.Files[0].Path);
        Assert.AreEqual(content.Length, logger.Files[0].Size);
        Assert.AreEqual("txt", logger.Files[0].FileType);
        // Check that the hash for the content matches what we expect
        Assert.AreEqual(Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content))), logger.Files[0].Hash);
    }    
}