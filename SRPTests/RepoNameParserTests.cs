using NUnit.Framework;
using PipelineCommon.Helpers;

namespace SRPTests;

public class RepoNameParserTests
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
        var result = RepoNameParser.Parse(repoName);
        Assert.AreEqual(language, result.LanguageCode);
        Assert.AreEqual(book, result.BookId);
        Assert.AreEqual(resource, result.ResourceType);
        Assert.AreEqual(level, result.Level);
    }
}
