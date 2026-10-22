using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BTTWriterLib.Models;
using PipelineCommon.Models;
using PipelineCommon.Models.BusMessages;

namespace PipelineCommon.Helpers;

/// <summary>
/// Per-repo field extraction that mirrors the nightly GogsAnalysis job, so event-driven
/// PORT updates produce the same values as the DW-to-PORT import.
/// </summary>
public static class RepoAnalysisHelpers
{
    public record RepoNameMetadata(string? LanguageCode, string? BookId, string? ResourceType, string? Level);

    private static readonly Regex ChunkIdRegex = new(@"^\d+-\d+$", RegexOptions.Compiled);
    private static readonly Regex UploadedChunkPathRegex = new(@"^\d+/\d+\.txt$", RegexOptions.Compiled);

    /// <summary>
    /// Ordered patterns from GogsAnalysis gen_repo_name_metadata.go. The first match wins.
    /// Each index is the capture group for that field, or 0 when the pattern does not capture it.
    /// </summary>
    private static readonly (Regex Pattern, int Lang, int Book, int Resource, int Level)[] RepoNamePatterns =
    {
        (new Regex(@"^bible_([^_]+)_([^-_]+)_([^_]+)_l(\d+)$"), 1, 2, 3, 4),
        (new Regex(@"^bible_([^_]+)_([^-_]+)_text_([^_]+)$"), 1, 2, 3, 0),
        (new Regex(@"^([^_]+)_([^-_]+)_text_([^_]+)_(l|lev|level|level_)(\d+)$"), 1, 2, 3, 5),
        (new Regex(@"^([^_]+)_([^-_]+)_([^_]+)_(l|lev|level|level_)(\d+)$"), 1, 2, 3, 5),
        (new Regex(@"^([^_]+)_([^-_]+)_text_([^_]+)$"), 1, 2, 3, 0),
        (new Regex(@"^uw-(obs)(tn)-(.*)$"), 3, 1, 2, 0),
        (new Regex(@"^uw-(tw)-(.*)$"), 2, 0, 1, 0),
        (new Regex(@"^uw-(obs)-(.*)$"), 2, 0, 1, 0),
        (new Regex(@"^uw-([^-_]+)-(.*)$"), 2, 1, 0, 0),
        (new Regex(@"^uw-([^-_]+)_([^-_]+)-(.*)$"), 3, 1, 2, 0),
        (new Regex(@"^([^_]+)_bible_([^_]+)$"), 1, 0, 2, 0),
        (new Regex(@"^([^_]+)_([^-_]+)_([^_]+)_([^_]+)$"), 1, 2, 3, 4),
        (new Regex(@"^([^_]+)_([^-_]+)_([^_]+)$"), 1, 2, 3, 0),
    };

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

            string? Group(int index) => index == 0 ? null : match.Groups[index].Value;
            return new RepoNameMetadata(Group(lang), Group(book), Group(resource), Group(level));
        }

        return new RepoNameMetadata(null, null, null, null);
    }

    /// <summary>
    /// Builds the analysis fields for one repo. Manifest values win over values parsed from the repo name.
    /// </summary>
    /// <param name="bttWriterManifest">The BTT Writer manifest, when the repo is a BTT Writer project.</param>
    public static RepoAnalysisFields BuildAnalysis(WACSMessage message, RepoIdentificationResult details,
        IZipFileSystem fileSystem, string? basePath, BTTWriterManifest? bttWriterManifest)
    {
        var fromName = ParseRepoName(message.Repo);
        var container = details.ResourceContainer;

        string? bookId;
        string? resourceType;
        IEnumerable<string>? translators;
        if (bttWriterManifest != null)
        {
            bookId = bttWriterManifest.project?.id;
            resourceType = bttWriterManifest.resource?.id;
            translators = bttWriterManifest.translators;
        }
        else
        {
            // Multi-book containers have no single book, so only a one-project container sets it.
            bookId = container?.projects is { Length: 1 } projects ? projects[0].identifier : null;
            resourceType = container?.dublin_core?.identifier;
            translators = container?.dublin_core?.contributor;
        }

        return new RepoAnalysisFields
        {
            BookId = FirstNonEmpty(bookId, fromName.BookId),
            ResourceType = FirstNonEmpty(resourceType, fromName.ResourceType),
            Translators = NormalizeTranslators(translators),
            LastCommitDate = message.LatestCommit?.Timestamp,
            FinishedChunks = CountFinishedChunks(bttWriterManifest?.finished_chunks),
            UploadedChunks = CountUploadedChunks(fileSystem, basePath),
        };
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>
    /// Counts chunk files laid out as <c>{chapter}/{verse}.txt</c> directly under <paramref name="basePath"/>.
    /// </summary>
    public static int CountUploadedChunks(IZipFileSystem fileSystem, string? basePath)
    {
        var prefix = string.IsNullOrEmpty(basePath) ? string.Empty : basePath.TrimEnd('/') + "/";
        return fileSystem.GetAllFiles(".txt")
            .Where(f => f.StartsWith(prefix))
            .Count(f => UploadedChunkPathRegex.IsMatch(f[prefix.Length..]));
    }

    /// <summary>
    /// Counts finished chunk ids shaped like <c>01-01</c>.
    /// </summary>
    public static int CountFinishedChunks(IEnumerable<string>? finishedChunks)
    {
        return finishedChunks?.Count(c => c != null && ChunkIdRegex.IsMatch(c)) ?? 0;
    }

    /// <summary>
    /// Joins translator names with <c>"; "</c>. Commas are removed and newlines become spaces because they break the DW importer.
    /// </summary>
    public static string NormalizeTranslators(IEnumerable<string>? translators)
    {
        if (translators == null)
        {
            return string.Empty;
        }

        return string.Join("; ", translators
            .Where(t => t != null)
            .Select(t => t.Trim().Replace(",", string.Empty).Replace("\n", " "))
            .Where(t => t.Length > 0));
    }
}
