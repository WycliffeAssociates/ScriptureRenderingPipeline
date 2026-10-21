using Microsoft.Xrm.Sdk;
using PipelineCommon.Models.BusMessages;

namespace VerseReportingProcessor;

/// <summary>
/// Values resolved for one <c>wa_translationrepo</c> write. Lookups are resolved by the caller so this stays pure.
/// </summary>
public class TranslationRepoValues
{
    public int ExpectedVerses { get; set; }
    public int ActualVerses { get; set; }
    public EntityReference? Language { get; set; }
    public RepoAnalysisFields? Analysis { get; set; }
    public EntityReference? Book { get; set; }
    public int? ExpectedChunks { get; set; }
}

/// <summary>
/// Builds the <c>wa_translationrepo</c> entities sent to PORT. Matches GogsImporter's rules so the nightly
/// import and event-driven updates agree: override flags protect their field, and only changed attributes are sent.
/// </summary>
public static class TranslationRepoEntityBuilder
{
    public const string EntityName = "wa_translationrepo";

    /// <summary>
    /// Dataverse rejects dates before this.
    /// </summary>
    public static readonly DateTime MinimumDate = new(1753, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Resource types that have a chunk definition in the chunk container.
    /// </summary>
    public static readonly HashSet<string> ChunkedResourceTypes = new(StringComparer.OrdinalIgnoreCase) { "ulb", "udb", "reg" };

    /// <summary>
    /// Columns to retrieve on the existing record so that <see cref="BuildUpdate"/> can compare and respect overrides.
    /// </summary>
    public static readonly string[] ExistingColumns =
    {
        "wa_expectedverses", "wa_actualverses", "wa_wacsid",
        "wa_language", "wa_language_override",
        "wa_book", "wa_book_id_override",
        "wa_resource_type", "wa_resource_type_override",
        "wa_level", "wa_level_override",
        "wa_translators", "wa_chunks_finished", "wa_chunks_uploaded", "wa_chunks_expected", "wa_lastcommitdate",
    };

    /// <summary>
    /// Builds a sparse update for <paramref name="existing"/>. Returns null when nothing changed.
    /// </summary>
    /// <param name="repoId">The WACS repo id, written when the record was matched without <c>wa_wacsid</c>.</param>
    public static Entity? BuildUpdate(Entity existing, TranslationRepoValues values, int? repoId)
    {
        var update = new Entity(EntityName, existing.Id);

        SetIfChanged(update, existing, "wa_expectedverses", values.ExpectedVerses);
        SetIfChanged(update, existing, "wa_actualverses", values.ActualVerses);
        if (repoId != null)
        {
            SetIfChanged(update, existing, "wa_wacsid", repoId.Value);
        }

        if (values.Language != null && !IsOverridden(existing, "wa_language_override"))
        {
            SetIfChanged(update, existing, "wa_language", values.Language);
        }

        var analysis = values.Analysis;
        if (analysis != null)
        {
            if (values.Book != null && !IsOverridden(existing, "wa_book_id_override"))
            {
                SetIfChanged(update, existing, "wa_book", values.Book);
            }
            if (!string.IsNullOrWhiteSpace(analysis.ResourceType) && !IsOverridden(existing, "wa_resource_type_override"))
            {
                SetIfChanged(update, existing, "wa_resource_type", analysis.ResourceType);
            }
            if (!string.IsNullOrWhiteSpace(analysis.Level) && !IsOverridden(existing, "wa_level_override"))
            {
                SetIfChanged(update, existing, "wa_level", analysis.Level);
            }

            SetIfChanged(update, existing, "wa_translators", analysis.Translators);
            SetIfChanged(update, existing, "wa_chunks_finished", analysis.FinishedChunks);
            SetIfChanged(update, existing, "wa_chunks_uploaded", analysis.UploadedChunks);
            if (values.ExpectedChunks != null)
            {
                SetIfChanged(update, existing, "wa_chunks_expected", values.ExpectedChunks.Value);
            }

            var commitDate = ToPortDate(analysis.LastCommitDate);
            if (commitDate != null)
            {
                SetIfChanged(update, existing, "wa_lastcommitdate", commitDate.Value);
            }
        }

        return update.Attributes.Count == 0 ? null : update;
    }

    /// <summary>
    /// Builds a new record. New records have no override flags, so every known value is written.
    /// </summary>
    public static Entity BuildCreate(TranslationRepoValues values, int? repoId, string user, string repo, string? repoUrl)
    {
        var entity = new Entity(EntityName, Guid.NewGuid())
        {
            ["wa_actualverses"] = values.ActualVerses,
            ["wa_expectedverses"] = values.ExpectedVerses,
            ["wa_wacsid"] = repoId,
            ["wa_user_id"] = user,
            ["wa_repo_id"] = repo,
            ["wa_source_system"] = "WACS",
            ["wa_language"] = values.Language,
            ["wa_url"] = repoUrl,
            ["wa_name"] = $"WACS/{user}/{repo}"
        };

        var analysis = values.Analysis;
        if (analysis == null)
        {
            return entity;
        }

        if (values.Book != null)
        {
            entity["wa_book"] = values.Book;
        }
        if (!string.IsNullOrWhiteSpace(analysis.ResourceType))
        {
            entity["wa_resource_type"] = analysis.ResourceType;
        }
        if (!string.IsNullOrWhiteSpace(analysis.Level))
        {
            entity["wa_level"] = analysis.Level;
        }
        entity["wa_translators"] = analysis.Translators;
        entity["wa_chunks_finished"] = analysis.FinishedChunks;
        entity["wa_chunks_uploaded"] = analysis.UploadedChunks;
        if (values.ExpectedChunks != null)
        {
            entity["wa_chunks_expected"] = values.ExpectedChunks.Value;
        }
        var commitDate = ToPortDate(analysis.LastCommitDate);
        if (commitDate != null)
        {
            entity["wa_lastcommitdate"] = commitDate.Value;
        }

        return entity;
    }

    /// <summary>
    /// Converts a commit date to UTC, or null when Dataverse can't store it.
    /// </summary>
    public static DateTime? ToPortDate(DateTime? date)
    {
        if (date == null)
        {
            return null;
        }
        var utc = date.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(date.Value, DateTimeKind.Utc)
            : date.Value.ToUniversalTime();
        return utc < MinimumDate ? null : utc;
    }

    private static bool IsOverridden(Entity existing, string overrideFlag) =>
        existing.Contains(overrideFlag) && existing[overrideFlag] is true;

    private static void SetIfChanged(Entity update, Entity existing, string attribute, object? value)
    {
        var current = existing.Contains(attribute) ? existing[attribute] : null;
        if (!AreEqual(current, value))
        {
            update[attribute] = value;
        }
    }

    private static bool AreEqual(object? current, object? value)
    {
        switch (current, value)
        {
            case (EntityReference a, EntityReference b):
                return a.Id == b.Id;
            // Dataverse stores dates to the second.
            case (DateTime a, DateTime b):
                return Math.Abs((a.ToUniversalTime() - b.ToUniversalTime()).TotalSeconds) < 1;
            // Dataverse returns empty strings as missing.
            case (null, string s):
                return s.Length == 0;
            case (string s, null):
                return s.Length == 0;
            default:
                return Equals(current, value);
        }
    }
}
