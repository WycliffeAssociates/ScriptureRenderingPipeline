using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using NUnit.Framework;
using PipelineCommon.Helpers;
using ScriptureRenderingPipelineWorker;
using ScriptureRenderingPipelineWorker.Models;
using SRPTests.TestHelpers;

namespace SRPTests;

/// <summary>
/// Tests that the wrapper encodes each file once and that the bytes logged are the bytes written
/// </summary>
public class OutputAndLoggingWrapperTests
{
    // Accents, an en dash and Greek so UTF-8 byte counts differ from character counts
    private const string NonAsciiHtml = "<p>Génesis 1 – ἐν ἀρχῇ & \"quotes\"</p>";

    private static JsonTypeInfo<T> TypeInfo<T>() =>
        (JsonTypeInfo<T>)JsonSerializerOptions.Default.GetTypeInfo(typeof(T));

    /// <summary>
    /// Verify that text is written as UTF-8 and the size and hash that get logged describe exactly those bytes
    /// </summary>
    [Test]
    public async Task TestWriteAllTextLogsTheBytesItWrites()
    {
        var output = new FakeOutputInterface();
        var logger = new FileTrackingLogger("https://example.org/u/user/repo", RepoType.Bible);
        var wrapper = new OutputAndLoggingWrapper(output, logger);

        await wrapper.WriteAllTextAsync("GEN/1.html", NonAsciiHtml, new() { ["book"] = "GEN" });

        var written = output.Bytes["GEN/1.html"];
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(NonAsciiHtml), written);
        Assert.AreEqual(NonAsciiHtml, output.Files["GEN/1.html"]);

        var logged = logger.Files[0];
        Assert.AreEqual(written.Length, logged.Size);
        Assert.AreNotEqual(NonAsciiHtml.Length, logged.Size);
        Assert.AreEqual(Convert.ToBase64String(SHA256.HashData(written)), logged.Hash);
        Assert.AreEqual("GEN", logged.Book);
        Assert.AreEqual(1, logged.Chapter);
    }

    /// <summary>
    /// Verify that serializing straight to UTF-8 gives the same bytes as serializing to a string and encoding it,
    /// so switching to it doesn't change any output, including how HTML and non-ASCII text are escaped
    /// </summary>
    [Test]
    public async Task TestWriteJsonMatchesSerializingToAString()
    {
        var output = new FakeOutputInterface();
        var logger = new FileTrackingLogger("https://example.org/u/user/repo", RepoType.Bible);
        var wrapper = new OutputAndLoggingWrapper(output, logger);
        var book = new OutputBook
        {
            Slug = "GEN",
            Label = "Génesis",
            LastRendered = "2026-10-15T00:00:00.0000000Z",
            Chapters = new List<OutputChapters>
            {
                new() { Number = "1", Label = "1", Content = NonAsciiHtml, ByteCount = Encoding.UTF8.GetByteCount(NonAsciiHtml) }
            }
        };
        var typeInfo = TypeInfo<OutputBook>();

        await wrapper.WriteJsonAsync("GEN/whole.json", book, typeInfo);

        var expected = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(book, typeInfo));
        CollectionAssert.AreEqual(expected, output.Bytes["GEN/whole.json"]);
        Assert.AreEqual(expected.Length, logger.Files[0].Size);
        Assert.AreEqual(Convert.ToBase64String(SHA256.HashData(expected)), logger.Files[0].Hash);
    }

    /// <summary>
    /// Verify that writing still works when there is no logger
    /// </summary>
    [Test]
    public async Task TestWritesWithoutALogger()
    {
        var output = new FakeOutputInterface();
        var wrapper = new OutputAndLoggingWrapper(output, null);

        await wrapper.WriteAllTextAsync("index.html", NonAsciiHtml);
        await wrapper.WriteJsonAsync("titles.json", new Dictionary<string, string> { ["a"] = "b" },
            TypeInfo<Dictionary<string, string>>());

        Assert.AreEqual(NonAsciiHtml, output.Files["index.html"]);
        Assert.AreEqual("{\"a\":\"b\"}", output.Files["titles.json"]);
    }
}
