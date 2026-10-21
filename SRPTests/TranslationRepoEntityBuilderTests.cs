using System;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;
using PipelineCommon.Models.BusMessages;
using VerseReportingProcessor;

namespace SRPTests;

public class TranslationRepoEntityBuilderTests
{
    private static readonly DateTime CommitDate = new(2015, 5, 5, 0, 0, 0, DateTimeKind.Utc);
    private readonly EntityReference _language = new("wa_language", Guid.NewGuid());
    private readonly EntityReference _book = new("wa_book", Guid.NewGuid());

    private TranslationRepoValues CreateValues()
    {
        return new TranslationRepoValues
        {
            ExpectedVerses = 100,
            ActualVerses = 50,
            Language = _language,
            Book = _book,
            ExpectedChunks = 15,
            Analysis = new RepoAnalysisFields
            {
                BookId = "mat",
                ResourceType = "ulb",
                Level = "1",
                Translators = "aaron; beth",
                LastCommitDate = CommitDate,
                FinishedChunks = 5,
                UploadedChunks = 10
            }
        };
    }

    private Entity CreateExistingMatching()
    {
        return new Entity(TranslationRepoEntityBuilder.EntityName, Guid.NewGuid())
        {
            ["wa_expectedverses"] = 100,
            ["wa_actualverses"] = 50,
            ["wa_wacsid"] = 42,
            ["wa_language"] = new EntityReference("wa_language", _language.Id),
            ["wa_book"] = new EntityReference("wa_book", _book.Id),
            ["wa_resource_type"] = "ulb",
            ["wa_level"] = "1",
            ["wa_translators"] = "aaron; beth",
            ["wa_chunks_finished"] = 5,
            ["wa_chunks_uploaded"] = 10,
            ["wa_chunks_expected"] = 15,
            ["wa_lastcommitdate"] = CommitDate
        };
    }

    private static Entity CreateExistingStale(Guid id)
    {
        return new Entity(TranslationRepoEntityBuilder.EntityName, id)
        {
            ["wa_expectedverses"] = 1,
            ["wa_actualverses"] = 1,
            ["wa_language"] = new EntityReference("wa_language", Guid.NewGuid()),
            ["wa_book"] = new EntityReference("wa_book", Guid.NewGuid()),
            ["wa_resource_type"] = "something else",
            ["wa_level"] = "something else",
            ["wa_translators"] = "something else",
            ["wa_chunks_finished"] = 0,
            ["wa_chunks_uploaded"] = 0,
            ["wa_chunks_expected"] = 0,
            ["wa_lastcommitdate"] = new DateTime(1982, 8, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    /// <summary>
    /// Verify that an up-to-date record produces no update.
    /// </summary>
    [Test]
    public void TestBuildUpdateReturnsNullWhenNothingChanged()
    {
        Assert.IsNull(TranslationRepoEntityBuilder.BuildUpdate(CreateExistingMatching(), CreateValues(), null));
    }

    /// <summary>
    /// Verify that a stale record gets every field, and only the record id plus changed attributes are sent.
    /// </summary>
    [Test]
    public void TestBuildUpdateSetsChangedFields()
    {
        var existing = CreateExistingStale(Guid.NewGuid());
        var update = TranslationRepoEntityBuilder.BuildUpdate(existing, CreateValues(), 42);

        Assert.IsNotNull(update);
        Assert.AreEqual(existing.Id, update.Id);
        Assert.AreEqual(100, update["wa_expectedverses"]);
        Assert.AreEqual(50, update["wa_actualverses"]);
        Assert.AreEqual(42, update["wa_wacsid"]);
        Assert.AreEqual(_language.Id, ((EntityReference)update["wa_language"]).Id);
        Assert.AreEqual(_book.Id, ((EntityReference)update["wa_book"]).Id);
        Assert.AreEqual("ulb", update["wa_resource_type"]);
        Assert.AreEqual("1", update["wa_level"]);
        Assert.AreEqual("aaron; beth", update["wa_translators"]);
        Assert.AreEqual(5, update["wa_chunks_finished"]);
        Assert.AreEqual(10, update["wa_chunks_uploaded"]);
        Assert.AreEqual(15, update["wa_chunks_expected"]);
        Assert.AreEqual(CommitDate, update["wa_lastcommitdate"]);
    }

    /// <summary>
    /// Verify that only the attributes that differ are sent.
    /// </summary>
    [Test]
    public void TestBuildUpdateOnlySendsChangedAttributes()
    {
        var existing = CreateExistingMatching();
        existing["wa_chunks_uploaded"] = 3;

        var update = TranslationRepoEntityBuilder.BuildUpdate(existing, CreateValues(), null);

        Assert.IsNotNull(update);
        Assert.AreEqual(1, update.Attributes.Count);
        Assert.AreEqual(10, update["wa_chunks_uploaded"]);
    }

    /// <summary>
    /// Verify that each override flag protects its field and leaves the others alone.
    /// </summary>
    [TestCase("wa_language_override", "wa_language")]
    [TestCase("wa_book_id_override", "wa_book")]
    [TestCase("wa_resource_type_override", "wa_resource_type")]
    [TestCase("wa_level_override", "wa_level")]
    public void TestBuildUpdateRespectsOverride(string overrideFlag, string protectedField)
    {
        var existing = CreateExistingStale(Guid.NewGuid());
        existing[overrideFlag] = true;

        var update = TranslationRepoEntityBuilder.BuildUpdate(existing, CreateValues(), null);

        Assert.IsNotNull(update);
        Assert.IsFalse(update.Contains(protectedField));
        Assert.AreEqual(10, update["wa_chunks_uploaded"]);
        Assert.AreEqual("aaron; beth", update["wa_translators"]);
    }

    /// <summary>
    /// Verify that an override flag set to false does not protect its field.
    /// </summary>
    [Test]
    public void TestBuildUpdateIgnoresFalseOverride()
    {
        var existing = CreateExistingStale(Guid.NewGuid());
        existing["wa_resource_type_override"] = false;

        var update = TranslationRepoEntityBuilder.BuildUpdate(existing, CreateValues(), null);

        Assert.AreEqual("ulb", update["wa_resource_type"]);
    }

    /// <summary>
    /// Verify that messages without analysis only touch verse counts, and that unknown values are not cleared.
    /// </summary>
    [Test]
    public void TestBuildUpdateWithoutAnalysis()
    {
        var existing = CreateExistingStale(Guid.NewGuid());
        var values = CreateValues();
        values.Analysis = null;
        values.Language = null;

        var update = TranslationRepoEntityBuilder.BuildUpdate(existing, values, null);

        Assert.IsNotNull(update);
        Assert.AreEqual(2, update.Attributes.Count);
        Assert.AreEqual(100, update["wa_expectedverses"]);
        Assert.AreEqual(50, update["wa_actualverses"]);
    }

    /// <summary>
    /// Verify that missing book, expected chunks, level, and dates Dataverse can't store are skipped.
    /// </summary>
    [Test]
    public void TestBuildUpdateSkipsUnknownValues()
    {
        var existing = CreateExistingStale(Guid.NewGuid());
        var values = CreateValues();
        values.Book = null;
        values.ExpectedChunks = null;
        values.Analysis.Level = null;
        values.Analysis.LastCommitDate = new DateTime(1700, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var update = TranslationRepoEntityBuilder.BuildUpdate(existing, values, null);

        Assert.IsFalse(update.Contains("wa_book"));
        Assert.IsFalse(update.Contains("wa_chunks_expected"));
        Assert.IsFalse(update.Contains("wa_level"));
        Assert.IsFalse(update.Contains("wa_lastcommitdate"));
    }

    /// <summary>
    /// Verify that an empty translator string matches a missing value in PORT.
    /// </summary>
    [Test]
    public void TestBuildUpdateTreatsEmptyTranslatorsAsMissing()
    {
        var existing = CreateExistingMatching();
        existing.Attributes.Remove("wa_translators");
        var values = CreateValues();
        values.Analysis.Translators = string.Empty;

        Assert.IsNull(TranslationRepoEntityBuilder.BuildUpdate(existing, values, null));
    }

    /// <summary>
    /// Verify that a new record gets identity, verse counts, and analysis fields.
    /// </summary>
    [Test]
    public void TestBuildCreate()
    {
        var entity = TranslationRepoEntityBuilder.BuildCreate(CreateValues(), 42, "user", "en_mat_text_ulb_l1", "https://example.com/user/en_mat_text_ulb_l1");

        Assert.AreEqual(TranslationRepoEntityBuilder.EntityName, entity.LogicalName);
        Assert.AreEqual(42, entity["wa_wacsid"]);
        Assert.AreEqual("user", entity["wa_user_id"]);
        Assert.AreEqual("en_mat_text_ulb_l1", entity["wa_repo_id"]);
        Assert.AreEqual("WACS", entity["wa_source_system"]);
        Assert.AreEqual("WACS/user/en_mat_text_ulb_l1", entity["wa_name"]);
        Assert.AreEqual(100, entity["wa_expectedverses"]);
        Assert.AreEqual(50, entity["wa_actualverses"]);
        Assert.AreEqual(_language, entity["wa_language"]);
        Assert.AreEqual(_book, entity["wa_book"]);
        Assert.AreEqual("ulb", entity["wa_resource_type"]);
        Assert.AreEqual("1", entity["wa_level"]);
        Assert.AreEqual("aaron; beth", entity["wa_translators"]);
        Assert.AreEqual(5, entity["wa_chunks_finished"]);
        Assert.AreEqual(10, entity["wa_chunks_uploaded"]);
        Assert.AreEqual(15, entity["wa_chunks_expected"]);
        Assert.AreEqual(CommitDate, entity["wa_lastcommitdate"]);
    }

    /// <summary>
    /// Verify that a new record without analysis only has identity and verse counts.
    /// </summary>
    [Test]
    public void TestBuildCreateWithoutAnalysis()
    {
        var values = CreateValues();
        values.Analysis = null;

        var entity = TranslationRepoEntityBuilder.BuildCreate(values, 42, "user", "repo", null);

        Assert.IsFalse(entity.Contains("wa_book"));
        Assert.IsFalse(entity.Contains("wa_translators"));
        Assert.IsFalse(entity.Contains("wa_chunks_expected"));
        Assert.AreEqual(100, entity["wa_expectedverses"]);
    }

    /// <summary>
    /// Verify that dates are converted to UTC and that dates before 1753 are dropped.
    /// </summary>
    [Test]
    public void TestToPortDate()
    {
        Assert.IsNull(TranslationRepoEntityBuilder.ToPortDate(null));
        Assert.IsNull(TranslationRepoEntityBuilder.ToPortDate(new DateTime(1752, 12, 31, 0, 0, 0, DateTimeKind.Utc)));
        var unspecified = new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);
        Assert.AreEqual(DateTimeKind.Utc, TranslationRepoEntityBuilder.ToPortDate(unspecified)!.Value.Kind);
        Assert.AreEqual(unspecified.Ticks, TranslationRepoEntityBuilder.ToPortDate(unspecified)!.Value.Ticks);
    }
}
