using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PipelineCommon.Helpers;

/// <summary>
/// Per-repo field extraction that mirrors the nightly GogsAnalysis job, so event-driven
/// PORT updates produce the same values as the DW-to-PORT import. Book, resource type, and
/// finished chunks come from <see cref="Utils.GetRepoInformation"/>.
/// </summary>
public static partial class RepoAnalysisHelpers
{
    public record RepoNameMetadata(string? LanguageCode, string? BookId, string? ResourceType, string? Level);

    [GeneratedRegex(@"^\d+-\d+$")]
    private static partial Regex ChunkIdRegex { get; }

    [GeneratedRegex(@"^\d+/\d+\.txt$")]
    private static partial Regex UploadedChunkPathRegex { get; }

    [GeneratedRegex(@"^bible_([^_]+)_([^-_]+)_([^_]+)_l(\d+)$")]
    private static partial Regex BibleWithLevelPattern { get; }

    [GeneratedRegex(@"^bible_([^_]+)_([^-_]+)_text_([^_]+)$")]
    private static partial Regex BibleTextPattern { get; }

    [GeneratedRegex(@"^([^_]+)_([^-_]+)_text_([^_]+)_(l|lev|level|level_)(\d+)$")]
    private static partial Regex TextWithLevelPattern { get; }

    [GeneratedRegex(@"^([^_]+)_([^-_]+)_([^_]+)_(l|lev|level|level_)(\d+)$")]
    private static partial Regex ResourceWithLevelPattern { get; }

    [GeneratedRegex(@"^([^_]+)_([^-_]+)_text_([^_]+)$")]
    private static partial Regex TextPattern { get; }

    [GeneratedRegex(@"^uw-(obs)(tn)-(.*)$")]
    private static partial Regex UwObsNotesPattern { get; }

    [GeneratedRegex(@"^uw-(tw)-(.*)$")]
    private static partial Regex UwWordsPattern { get; }

    [GeneratedRegex(@"^uw-(obs)-(.*)$")]
    private static partial Regex UwObsPattern { get; }

    [GeneratedRegex(@"^uw-([^-_]+)-(.*)$")]
    private static partial Regex UwBookPattern { get; }

    [GeneratedRegex(@"^uw-([^-_]+)_([^-_]+)-(.*)$")]
    private static partial Regex UwBookResourcePattern { get; }

    [GeneratedRegex(@"^([^_]+)_bible_([^_]+)$")]
    private static partial Regex WholeBiblePattern { get; }

    [GeneratedRegex(@"^([^_]+)_([^-_]+)_([^_]+)_([^_]+)$")]
    private static partial Regex FourPartPattern { get; }

    [GeneratedRegex(@"^([^_]+)_([^-_]+)_([^_]+)$")]
    private static partial Regex ThreePartPattern { get; }

    /// <summary>
    /// Ordered patterns from GogsAnalysis gen_repo_name_metadata.go. The first match wins.
    /// Each index is the capture group for that field, or 0 when the pattern does not capture it.
    /// </summary>
    private static readonly (Regex Pattern, int Lang, int Book, int Resource, int Level)[] RepoNamePatterns =
    [
        (BibleWithLevelPattern, 1, 2, 3, 4),
        (BibleTextPattern, 1, 2, 3, 0),
        (TextWithLevelPattern, 1, 2, 3, 5),
        (ResourceWithLevelPattern, 1, 2, 3, 5),
        (TextPattern, 1, 2, 3, 0),
        (UwObsNotesPattern, 3, 1, 2, 0),
        (UwWordsPattern, 2, 0, 1, 0),
        (UwObsPattern, 2, 0, 1, 0),
        (UwBookPattern, 2, 1, 0, 0),
        (UwBookResourcePattern, 3, 1, 2, 0),
        (WholeBiblePattern, 1, 0, 2, 0),
        (FourPartPattern, 1, 2, 3, 4),
        (ThreePartPattern, 1, 2, 3, 0),
    ];

    /// <summary>
    /// Parses language, book, resource type, and level out of a repo name. Unmatched names return all nulls.
    /// </summary>
    public static RepoNameMetadata ParseRepoName(string? repoName)
    {
        if (string.IsNullOrEmpty(repoName))
        {
            return new RepoNameMetadata(null, null, null, null);
        }

        foreach (var (pattern, lang, book, resource, level) in RepoNamePatterns)
        {
            var match = pattern.Match(repoName);
            if (!match.Success)
            {
                continue;
            }

            var group = (int index) => index == 0 ? null : match.Groups[index].Value;
            return new RepoNameMetadata(group(lang), group(book), group(resource), group(level));
        }

        return new RepoNameMetadata(null, null, null, null);
    }

    /// <summary>
    /// Counts chunk files laid out as <c>{chapter}/{verse}.txt</c> directly under <paramref name="basePath"/>.
    /// </summary>
    /// <returns>The chunk file count, or null when there are none, such as a whole Bible in USFM.</returns>
    public static int? CountUploadedChunks(IZipFileSystem fileSystem, string? basePath)
    {
        var prefix = string.IsNullOrEmpty(basePath) ? string.Empty : basePath.TrimEnd('/') + "/";
        var count = fileSystem.GetFiles(prefix, ".txt")
            .Count(f => UploadedChunkPathRegex.IsMatch(f[prefix.Length..]));
        return count == 0 ? null : count;
    }

    /// <summary>
    /// Counts finished chunk ids shaped like <c>01-01</c>.
    /// </summary>
    /// <returns>The finished chunk count, or null when there is no list of finished chunks.</returns>
    public static int? CountFinishedChunks(IEnumerable<string>? finishedChunks)
    {
        return finishedChunks?.Count(c => c != null && ChunkIdRegex.IsMatch(c));
    }

    /// <summary>
    /// Joins translator names with <c>"; "</c>. Commas are removed and newlines become spaces because they break the DW importer.
    /// </summary>
    /// <remarks>
    /// This path writes to PORT directly, but the nightly DW-to-PORT import writes the same field, so the
    /// values have to match or each run would overwrite the other.
    /// </remarks>
    /// <returns>The joined names, or null when there are none.</returns>
    public static string? NormalizeTranslators(IEnumerable<string>? translators)
    {
        if (translators == null)
        {
            return null;
        }

        var output = string.Join("; ", translators
            .Where(t => t != null)
            .Select(t => t.Trim().Replace(",", string.Empty).Replace("\n", " "))
            .Where(t => t.Length > 0));
        return output.Length == 0 ? null : output;
    }
}
