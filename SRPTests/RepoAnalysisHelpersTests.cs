using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using PipelineCommon.Helpers;
using SRPTests.TestHelpers;

namespace SRPTests;

public class RepoAnalysisHelpersTests
{
    /// <summary>
    /// Verify that only {chapter}/{verse}.txt files directly under the base path count as uploaded chunks,
    /// and that a repo with none gets null.
    /// </summary>
    [Test]
    public void TestCountUploadedChunks()
    {
        var fileSystem = new FakeZipFileSystem();
        fileSystem.AddFile("repo/01/01.txt", "");
        fileSystem.AddFile("repo/01/04.txt", "");
        fileSystem.AddFile("repo/02/01.txt", "");
        fileSystem.AddFile("repo/front/title.txt", "");
        fileSystem.AddFile("repo/01/title.txt", "");
        fileSystem.AddFile("repo/manifest.json", "");
        fileSystem.AddFile("repo/nested/01/01.txt", "");
        fileSystem.AddFile("other/01/01.txt", "");
        fileSystem.AddFile("repository/01/01.txt", "");

        Assert.AreEqual(3, RepoAnalysisHelpers.CountUploadedChunks(fileSystem, "repo"));
        Assert.AreEqual(3, RepoAnalysisHelpers.CountUploadedChunks(fileSystem, "repo/"));
        Assert.IsNull(RepoAnalysisHelpers.CountUploadedChunks(fileSystem, "empty"));
    }

    /// <summary>
    /// Verify that only ids shaped like chapter-verse count as finished chunks, and that no list gets null.
    /// </summary>
    [Test]
    public void TestCountFinishedChunks()
    {
        Assert.IsNull(RepoAnalysisHelpers.CountFinishedChunks(null));
        Assert.AreEqual(0, RepoAnalysisHelpers.CountFinishedChunks(new string[0]));
        Assert.AreEqual(2, RepoAnalysisHelpers.CountFinishedChunks(new[] { "01-01", "front-title", "02-05", null, "01-title" }));
    }

    /// <summary>
    /// Verify that translators are trimmed, have commas and newlines removed, and are joined with "; ",
    /// and that no names gets null.
    /// </summary>
    [Test]
    public void TestNormalizeTranslators()
    {
        Assert.IsNull(RepoAnalysisHelpers.NormalizeTranslators(null));
        Assert.IsNull(RepoAnalysisHelpers.NormalizeTranslators(new[] { " ", "", null }));
        Assert.AreEqual("Smith John; Jane\rDoe; Bob Ross",
            RepoAnalysisHelpers.NormalizeTranslators(new[] { " Smith, John ", "Jane\rDoe", "", null, "Bob\nRoss" }));
    }

    /// <summary>
    /// Verify that GetRepoInformation takes book, resource, translators, and finished chunks from a BTT Writer manifest.
    /// </summary>
    [Test]
    public async Task TestGetRepoInformationFromBTTWriterManifest()
    {
        var fileSystem = new FakeZipFileSystem();
        fileSystem.AddFile("repo/manifest.json", @"{
  ""project"": { ""id"": ""mrk"", ""name"": ""Mark"" },
  ""resource"": { ""id"": ""udb"", ""name"": ""Unlocked Dynamic Bible"" },
  ""target_language"": { ""id"": ""en"", ""name"": ""English"", ""direction"": ""ltr"" },
  ""translators"": [""Aaron"", ""Beth""],
  ""finished_chunks"": [""01-01"", ""01-03"", ""front-title""]
}");

        var result = await Utils.GetRepoInformation(NullLogger.Instance, fileSystem, "repo", "en_mat_text_ulb_l2");

        Assert.AreEqual(RepoFormat.BTTWriter, result.RepoFormat);
        Assert.AreEqual("mrk", result.BookId);
        Assert.AreEqual("udb", result.ResourceId);
        CollectionAssert.AreEqual(new[] { "Aaron", "Beth" }, result.ResourceContainer.dublin_core.contributor);
        Assert.AreEqual(2, RepoAnalysisHelpers.CountFinishedChunks(result.FinishedChunks));
    }

    /// <summary>
    /// Verify that GetRepoInformation sets the book only for a single-project resource container,
    /// and that repo-name values fill in what the container lacks.
    /// </summary>
    [Test]
    public async Task TestGetRepoInformationFromResourceContainer()
    {
        var single = new FakeZipFileSystem();
        single.AddFile("repo/manifest.yaml", @"dublin_core:
  identifier: reg
  contributor: [Charlie]
  language: { identifier: en, title: English, direction: ltr }
projects:
  - identifier: luk");
        var singleResult = await Utils.GetRepoInformation(NullLogger.Instance, single, "repo", "en_mat_text_ulb_l2");
        Assert.AreEqual("luk", singleResult.BookId);
        Assert.AreEqual("reg", singleResult.ResourceId);
        Assert.IsNull(singleResult.FinishedChunks);

        var multiple = new FakeZipFileSystem();
        multiple.AddFile("repo/manifest.yaml", @"dublin_core:
  language: { identifier: en, title: English, direction: ltr }
projects:
  - identifier: luk
  - identifier: jhn");
        var multipleResult = await Utils.GetRepoInformation(NullLogger.Instance, multiple, "repo", "en_mat_text_ulb_l2");
        Assert.AreEqual("mat", multipleResult.BookId);
        Assert.AreEqual("ulb", multipleResult.ResourceId);
    }
}
