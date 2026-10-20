using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PipelineCommon.Helpers;
using SRPTests.TestHelpers;

namespace SRPTests;

/// <summary>
/// Tests that files whose content is already in storage aren't uploaded again
/// </summary>
public class DirectAzureUploadTests
{
    private const string BasePath = "/u/user/repo";
    // Storage names don't keep the leading slash
    private const string StoredPath = "u/user/repo";
    private static readonly byte[] Content = Encoding.UTF8.GetBytes("<p>In the beginning</p>");

    [Test]
    public async Task TestUnchangedFileIsSkipped()
    {
        var container = new FakeBlobContainerClient();
        container.AddExisting($"{StoredPath}/GEN/1.html", "text/html", MD5.HashData(Content));
        var upload = new DirectAzureUpload(BasePath, container, new FakeLogger());
        upload.EnableSkipUnchangedFiles();

        await upload.WriteAllBytesAsync("GEN/1.html", Content);
        await upload.FinishAsync();

        Assert.IsEmpty(container.Uploads);
        Assert.AreEqual(1, upload.SkippedFiles);
        Assert.AreEqual(0, upload.UploadedFiles);
    }

    [Test]
    public async Task TestChangedFileIsUploadedWithItsHash()
    {
        var container = new FakeBlobContainerClient();
        container.AddExisting($"{StoredPath}/GEN/1.html", "text/html", MD5.HashData(Encoding.UTF8.GetBytes("old")));
        var upload = new DirectAzureUpload(BasePath, container, new FakeLogger());
        upload.EnableSkipUnchangedFiles();

        await upload.WriteAllBytesAsync("GEN/1.html", Content);
        await upload.FinishAsync();

        var headers = container.Uploads[$"{StoredPath}/GEN/1.html"].HttpHeaders;
        CollectionAssert.AreEqual(MD5.HashData(Content), headers.ContentHash);
        Assert.AreEqual("text/html", headers.ContentType);
        Assert.AreEqual(1, upload.UploadedFiles);
    }

    [Test]
    public async Task TestNewFileIsUploaded()
    {
        var container = new FakeBlobContainerClient();
        var upload = new DirectAzureUpload(BasePath, container, new FakeLogger());
        upload.EnableSkipUnchangedFiles();

        await upload.WriteAllBytesAsync("GEN/1.html", Content);
        await upload.FinishAsync();

        Assert.That(container.Uploads.ContainsKey($"{StoredPath}/GEN/1.html"));
    }

    /// <summary>
    /// Same bytes but a different content type still needs uploading so the header gets corrected
    /// </summary>
    [Test]
    public async Task TestContentTypeChangeIsUploaded()
    {
        var container = new FakeBlobContainerClient();
        container.AddExisting($"{StoredPath}/GEN/1.html", "application/octet-stream", MD5.HashData(Content));
        var upload = new DirectAzureUpload(BasePath, container, new FakeLogger());
        upload.EnableSkipUnchangedFiles();

        await upload.WriteAllBytesAsync("GEN/1.html", Content);
        await upload.FinishAsync();

        Assert.AreEqual(1, container.Uploads.Count);
    }

    /// <summary>
    /// Blobs uploaded before hashes were set may not have one, those get uploaded once to fill it in
    /// </summary>
    [Test]
    public async Task TestExistingFileWithoutHashIsUploaded()
    {
        var container = new FakeBlobContainerClient();
        container.AddExisting($"{StoredPath}/GEN/1.html", "text/html", null);
        var upload = new DirectAzureUpload(BasePath, container, new FakeLogger());
        upload.EnableSkipUnchangedFiles();

        await upload.WriteAllBytesAsync("GEN/1.html", Content);
        await upload.FinishAsync();

        Assert.AreEqual(1, container.Uploads.Count);
    }

    [Test]
    public async Task TestEverythingUploadsWithoutSkipEnabled()
    {
        var container = new FakeBlobContainerClient();
        container.AddExisting($"{StoredPath}/GEN/1.html", "text/html", MD5.HashData(Content));
        var upload = new DirectAzureUpload(BasePath, container, new FakeLogger());

        await upload.WriteAllBytesAsync("GEN/1.html", Content);
        await upload.FinishAsync();

        Assert.AreEqual(1, container.Uploads.Count);
    }

    [Test]
    public async Task TestListingFailureFallsBackToUploading()
    {
        var container = new FakeBlobContainerClient() { ThrowOnList = true };
        container.AddExisting($"{StoredPath}/GEN/1.html", "text/html", MD5.HashData(Content));
        var log = new FakeLogger();
        var upload = new DirectAzureUpload(BasePath, container, log);
        upload.EnableSkipUnchangedFiles();

        await upload.WriteAllBytesAsync("GEN/1.html", Content);
        await upload.FinishAsync();

        Assert.AreEqual(1, container.Uploads.Count);
        Assert.AreEqual(1, log.WarningMessages.Count);
    }

    /// <summary>
    /// The listing shouldn't pull in sibling repos like /u/user/repo2 that share the same start
    /// </summary>
    [Test]
    public async Task TestListingIsLimitedToThisRepo()
    {
        var container = new FakeBlobContainerClient();
        container.AddExisting("u/user/repo2/GEN/1.html", "text/html", MD5.HashData(Content));
        var upload = new DirectAzureUpload(BasePath, container, new FakeLogger());
        upload.EnableSkipUnchangedFiles();

        await upload.WriteAllBytesAsync("GEN/1.html", Content);
        await upload.FinishAsync();

        Assert.AreEqual($"{StoredPath}/", container.ListedPrefix);
        Assert.AreEqual(1, container.Uploads.Count);
    }
}
