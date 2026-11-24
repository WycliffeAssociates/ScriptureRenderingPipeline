using System.Text.RegularExpressions;

namespace PipelineCommon.Helpers;

public record RepoNameMetadata(string? LanguageCode, string? BookId, string? ResourceType, string? Level);

/// <summary>
/// Parses language, book, resource type, and level out of a repo name, using the same patterns as the
/// nightly GogsAnalysis job. Used when a repo has no manifest to say what it is.
/// </summary>
public static partial class RepoNameParser
{
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
    public static RepoNameMetadata Parse(string? repoName)
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
}
