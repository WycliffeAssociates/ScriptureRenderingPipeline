using System;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;
using PipelineCommon.Helpers;
using PipelineCommon.Models;

namespace SRPTests;

/// <summary>
/// Tests for the cached language lookup. The cache is shared for the whole process so every test uses its
/// own path as the cache key.
/// </summary>
public class TranslationDatabaseInterfaceTests
{
    private static string UniquePath() => $"https://example.org/{Guid.NewGuid()}/langnames.json";

    private static TranslationDatabaseLanguage Language(string code, string name) =>
        new() { LanguageCode = code, LanguageName = name };

    /// <summary>
    /// Verify that the list is only downloaded once no matter how many lookups use it
    /// </summary>
    [Test]
    public async Task TestOnlyDownloadsOnce()
    {
        var path = UniquePath();
        var downloads = 0;
        Task<TranslationDatabaseLanguage[]> Download()
        {
            downloads++;
            return Task.FromResult(new[] { Language("en", "English"), Language("es", "Español") });
        }

        Assert.AreEqual("English", (await TranslationDatabaseInterface.GetLanguageAsync(path, "en", Download))?.LanguageName);
        Assert.AreEqual("Español", (await TranslationDatabaseInterface.GetLanguageAsync(path, "es", Download))?.LanguageName);
        Assert.AreEqual("English", (await TranslationDatabaseInterface.GetLanguageAsync(path, "en", Download))?.LanguageName);
        Assert.AreEqual(1, downloads);
    }

    /// <summary>
    /// Verify that a code that isn't in the list gives back null
    /// </summary>
    [Test]
    public async Task TestMissingLanguageReturnsNull()
    {
        var result = await TranslationDatabaseInterface.GetLanguageAsync(UniquePath(), "xyz",
            () => Task.FromResult(new[] { Language("en", "English") }));
        Assert.IsNull(result);
    }

    /// <summary>
    /// Verify that when a code is listed twice the first one wins, which is what the old linear search returned
    /// </summary>
    [Test]
    public async Task TestDuplicateCodeKeepsFirst()
    {
        var result = await TranslationDatabaseInterface.GetLanguageAsync(UniquePath(), "en",
            () => Task.FromResult(new[] { Language("en", "First"), Language("en", "Second") }));
        Assert.AreEqual("First", result?.LanguageName);
    }

    /// <summary>
    /// Verify that entries without a language code are skipped instead of breaking the lookup
    /// </summary>
    [Test]
    public async Task TestEntriesWithoutCodeAreSkipped()
    {
        var result = await TranslationDatabaseInterface.GetLanguageAsync(UniquePath(), "en",
            () => Task.FromResult(new[] { Language(null!, "No code"), Language("en", "English") }));
        Assert.AreEqual("English", result?.LanguageName);
    }

    /// <summary>
    /// Verify that a failed download isn't cached so the next lookup tries again
    /// </summary>
    [Test]
    public async Task TestFailedDownloadIsNotCached()
    {
        var path = UniquePath();
        Assert.ThrowsAsync<HttpRequestException>(() => TranslationDatabaseInterface.GetLanguageAsync(path, "en",
            () => throw new HttpRequestException("down")));

        var result = await TranslationDatabaseInterface.GetLanguageAsync(path, "en",
            () => Task.FromResult(new[] { Language("en", "English") }));
        Assert.AreEqual("English", result?.LanguageName);
    }
}
