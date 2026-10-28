using System;
using System.Threading.Tasks;
using BTTWriterLib.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using PipelineCommon.Helpers;
using PipelineCommon.Models;
using PipelineCommon.Models.BusMessages;
using PipelineCommon.Models.ResourceContainer;
using SRPTests.TestHelpers;

namespace SRPTests;

public class RepoAnalysisHelpersTests
{
    /// <summary>
    /// Verify that repo names parse the same way GogsAnalysis gen_repo_name_metadata does.
    /// </summary>
    [TestCase("ilo_1ti_text_ulb_l3", "ilo", "1ti", "ulb", "3")]
    [TestCase("es_mrk_text_reg", "es", "mrk", "reg", null)]
    [TestCase("bible_en_gen_ulb_l2", "en", "gen", "ulb", "2")]
    [TestCase("uw-obs-en", "en", null, "obs", null)]
    [TestCase("uw-tw-fr", "fr", null, "tw", null)]
    [TestCase("ilo_1ti_text_ulb_l3-bad-manifest-lang-code", null, null, null, null)]
    [TestCase("te_ust", null, null, null, null)]
    [TestCase("", null, null, null, null)]
    [TestCase(null, null, null, null, null)]
    public void TestParseRepoName(string repoName, string language, string book, string resource, string level)
    {
        var result = RepoAnalysisHelpers.ParseRepoName(repoName);
        Assert.AreEqual(language, result.LanguageCode);
        Assert.AreEqual(book, result.BookId);
        Assert.AreEqual(resource, result.ResourceType);
        Assert.AreEqual(level, result.Level);
    }

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
    /// Verify that a BTT Writer manifest supplies book, resource, translators, and finished chunks,
    /// and that the level comes from the repo name.
    /// </summary>
    [Test]
    public void TestBuildAnalysisFromBTTWriterManifest()
    {
        var fileSystem = new FakeZipFileSystem();
        fileSystem.AddFile("repo/01/01.txt", "");
        fileSystem.AddFile("repo/01/03.txt", "");
        var timestamp = new DateTime(2024, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var message = new WACSMessage
        {
            Repo = "en_mat_text_ulb_l2",
            LatestCommit = new SimplifiedCommit { Timestamp = timestamp }
        };
        var manifest = new BTTWriterManifest
        {
            project = new IdNameCombo { id = "mrk" },
            resource = new IdNameCombo { id = "udb" },
            translators = new[] { "Aaron", "Beth" },
            finished_chunks = new[] { "01-01", "01-03", "front-title" }
        };

        var result = RepoAnalysisHelpers.BuildAnalysis(message, new RepoIdentificationResult(), fileSystem, "repo", manifest);

        Assert.AreEqual("mrk", result.BookId);
        Assert.AreEqual("udb", result.ResourceType);
        Assert.AreEqual("Aaron; Beth", result.Translators);
        Assert.AreEqual(timestamp, result.LastCommitDate);
        Assert.AreEqual(2, result.FinishedChunks);
        Assert.AreEqual(2, result.UploadedChunks);
    }

    /// <summary>
    /// Verify that a resource container supplies the book only when it has a single project,
    /// and that repo-name values fill in what the container lacks.
    /// </summary>
    [Test]
    public void TestBuildAnalysisFromResourceContainer()
    {
        var message = new WACSMessage { Repo = "en_mat_text_ulb_l2" };
        var details = new RepoIdentificationResult
        {
            ResourceContainer = new ResourceContainer
            {
                dublin_core = new DublinCore { identifier = "reg", contributor = new[] { "Charlie" } },
                projects = new[] { new Project { identifier = "luk" } }
            }
        };

        var single = RepoAnalysisHelpers.BuildAnalysis(message, details, new FakeZipFileSystem(), "repo", null);
        Assert.AreEqual("luk", single.BookId);
        Assert.AreEqual("reg", single.ResourceType);
        Assert.AreEqual("Charlie", single.Translators);
        Assert.IsNull(single.LastCommitDate);
        Assert.IsNull(single.FinishedChunks);
        Assert.IsNull(single.UploadedChunks);

        details.ResourceContainer.projects = new[] { new Project { identifier = "luk" }, new Project { identifier = "jhn" } };
        details.ResourceContainer.dublin_core.identifier = null;
        var multiple = RepoAnalysisHelpers.BuildAnalysis(message, details, new FakeZipFileSystem(), "repo", null);
        Assert.AreEqual("mat", multiple.BookId);
        Assert.AreEqual("ulb", multiple.ResourceType);
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
