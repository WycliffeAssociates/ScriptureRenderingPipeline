using System.Collections.Generic;
using System.Text.Json;
using BTTWriterCatalog.Helpers;
using BTTWriterCatalog.Models;
using NUnit.Framework;
using SRPTests.TestHelpers;

namespace SRPTests.BTTWriterCatalogTests;

/// <summary>
/// Pins the on the wire format of a repository's chunks.json so that it does not drift,
/// since these files are written by hand outside of this codebase
/// </summary>
public class ChunkFormatTests
{
    [Test]
    public void TestReadsCompactChunks()
    {
        var parsed = RepositoryChunks.Parse("{\"GEN\":{\"1\":[[1,5],[6,0]],\"2\":[[1,25]]}}", new FakeLogger());
        Assert.AreEqual(2, parsed["GEN"][1].Count, "Chapter one should have two chunks.");
        Assert.AreEqual(1, parsed["GEN"][1][0].StartingVerse, "First chunk should start at verse one.");
        Assert.AreEqual(5, parsed["GEN"][1][0].EndingVerse, "First chunk should end at verse five.");
        Assert.AreEqual(6, parsed["GEN"][1][1].StartingVerse, "Second chunk should start at verse six.");
        Assert.AreEqual(0, parsed["GEN"][1][1].EndingVerse, "An ending verse of zero means run to the end of the chapter.");
        Assert.AreEqual(25, parsed["GEN"][2][0].EndingVerse, "Chapter two should end at verse twenty five.");
    }

    [Test]
    public void TestRoundTripsCompactChunks()
    {
        // The write side is not used in the pipeline, so this is the only thing keeping the two directions in step
        var options = new JsonSerializerOptions() { Converters = { new VerseChunkArrayConverter() } };
        var json = "{\"GEN\":{\"1\":[[1,5],[6,0]]}}";
        Assert.AreEqual(json, JsonSerializer.Serialize(RepositoryChunks.Parse(json, new FakeLogger()), options),
            "Chunks should serialize back to arrays.");
    }

    [Test]
    public void TestRejectsMalformedChunks()
    {
        // The old object format would otherwise silently deserialize to chunks of (0,0)
        Assert.Throws<JsonException>(() => RepositoryChunks.Parse("{\"GEN\":{\"1\":[{\"StartingVerse\":1,\"EndingVerse\":5}]}}", new FakeLogger()),
            "The full property name format should no longer be accepted.");
        Assert.Throws<JsonException>(() => RepositoryChunks.Parse("{\"GEN\":{\"1\":[null]}}", new FakeLogger()),
            "A null chunk should be rejected rather than becoming a null entry.");
        Assert.Throws<JsonException>(() => RepositoryChunks.Parse("{\"GEN\":{\"1\":[[1]]}}", new FakeLogger()),
            "A chunk missing its ending verse should be rejected.");
        Assert.Throws<JsonException>(() => RepositoryChunks.Parse("{\"GEN\":{\"1\":[[1,5,9]]}}", new FakeLogger()),
            "A chunk with more than two numbers should be rejected.");
    }

    [Test]
    public void TestRejectsNegativeVerses()
    {
        var starting = Assert.Throws<JsonException>(
            () => RepositoryChunks.Parse("{\"GEN\":{\"1\":[[-1,5]]}}", new FakeLogger()),
            "A negative starting verse should be rejected.");
        Assert.IsTrue(starting.Message.Contains("starting verse"), "The error should say which verse number was bad.");
        Assert.Throws<JsonException>(() => RepositoryChunks.Parse("{\"GEN\":{\"1\":[[1,-5]]}}", new FakeLogger()),
            "A negative ending verse should be rejected.");
    }

    [Test]
    public void TestStorageChunkFormatIsUnaffected()
    {
        // The chunk definitions in blob storage still use the full property names, so the
        // converter must only apply where it is passed in explicitly
        var stored = JsonSerializer.Deserialize<Dictionary<int, List<VerseChunk>>>(
            "{\"1\":[{\"StartingVerse\":3,\"EndingVerse\":9}]}");
        Assert.AreEqual(3, stored[1][0].StartingVerse, "Stored chunks should still start at verse three.");
        Assert.AreEqual(9, stored[1][0].EndingVerse, "Stored chunks should still end at verse nine.");
    }

    [Test]
    public void TestBookKeysAreCaseInsensitive()
    {
        var parsed = RepositoryChunks.Parse("{\"gen\":{\"1\":[[1,5]]}}", new FakeLogger());
        Assert.IsTrue(parsed.ContainsKey("GEN"), "A lower case book should be found under its upper case abbreviation.");
        Assert.AreEqual(1, parsed["GEN"][1][0].StartingVerse, "The chunks for the book should be kept.");
    }

    [Test]
    public void TestDuplicateBooksTakeTheLastOne()
    {
        var log = new FakeLogger();
        var parsed = RepositoryChunks.Parse("{\"gen\":{\"1\":[[1,5]]},\"GEN\":{\"1\":[[6,0]]}}", log);
        Assert.AreEqual(1, parsed.Count, "The two spellings of the book should collapse into one entry.");
        Assert.AreEqual(6, parsed["GEN"][1][0].StartingVerse, "The last book listed should win.");
        Assert.AreEqual(1, log.WarningMessages.Count, "A duplicated book should be warned about.");
    }

    [Test]
    public void TestParseErrorSaysWhereTheProblemIs()
    {
        var ex = Assert.Throws<JsonException>(
            () => RepositoryChunks.Parse("{\"GEN\":{\"1\":[[1,\"5\"]]}}", new FakeLogger()),
            "A bad chunk should be rejected.");
        Assert.IsTrue(ex.Message.Contains("chunks.json"), "The error should name the file that could not be read.");
        Assert.IsTrue(ex.Message.Contains("$.GEN.1[0]"), "The error should say where in the file the problem is.");
    }

    [Test]
    public void TestParsesEmptyChunks()
    {
        Assert.AreEqual(0, RepositoryChunks.Parse("{}", new FakeLogger()).Count,
            "An empty chunks.json should give no chunks.");
        Assert.AreEqual(0, RepositoryChunks.Parse("null", new FakeLogger()).Count,
            "A null chunks.json should give no chunks rather than blowing up.");
    }
}
