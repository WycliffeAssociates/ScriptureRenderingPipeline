using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PipelineCommon.Helpers;

/// <summary>
/// Chunk and translator values that mirror the nightly GogsAnalysis job, so event-driven
/// PORT updates produce the same values as the DW-to-PORT import. Book, resource type, and
/// finished chunks come from <see cref="Utils.GetRepoInformation"/>, and repo names are
/// parsed by <see cref="RepoNameParser"/>.
/// </summary>
public static partial class RepoAnalysisHelpers
{
    [GeneratedRegex(@"^\d+-\d+$")]
    private static partial Regex ChunkIdRegex { get; }

    [GeneratedRegex(@"^\d+/\d+\.txt$")]
    private static partial Regex UploadedChunkPathRegex { get; }

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
    /// Joins translator names with <c>"; "</c>. Commas are removed and newlines become spaces to match GogsAnalysis,
    /// whose values GogsImporter writes to the same field nightly.
    /// </summary>
    /// <remarks>
    /// GogsImporter overwrites <c>wa_translators</c> whenever it differs and has no override flag for it, so if
    /// these values didn't match, each run would overwrite the other. The comma removal can go once GogsImporter
    /// stops writing translators.
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
