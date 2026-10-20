using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;

namespace PipelineCommon.Helpers;

public class DirectAzureUpload: IOutputInterface
{
    private readonly List<Task> _tasks = new();
    private readonly string _basePath;
    private readonly BlobContainerClient _client;
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger _log;
    private Task<Dictionary<string, BlobItemProperties>>? _existingBlobs;
    private int _uploadedFiles;
    private int _skippedFiles;

    /// <summary>
    /// Number of files that were sent to storage
    /// </summary>
    public int UploadedFiles => _uploadedFiles;

    /// <summary>
    /// Number of files that weren't sent because storage already had identical content
    /// </summary>
    public int SkippedFiles => _skippedFiles;

    public DirectAzureUpload(string basePath, BlobContainerClient uploadClient, ILogger log, int maxConcurrentUploads = 50)
    {
        _log = log;
        _basePath = basePath;
        _client = uploadClient;
        _semaphore = new SemaphoreSlim(maxConcurrentUploads);
    }
    public void Dispose()
    {
        _semaphore.Dispose();
    }

    /// <summary>
    /// List what is already stored under the base path so files whose content hasn't changed can be skipped.
    /// Call this before writing anything; without it every file is uploaded.
    /// </summary>
    public void EnableSkipUnchangedFiles()
    {
        _existingBlobs ??= ListExistingBlobsAsync();
    }

    private async Task<Dictionary<string, BlobItemProperties>> ListExistingBlobsAsync()
    {
        var output = new Dictionary<string, BlobItemProperties>();
        // The trailing slash stops this from also listing sibling repos whose names start with this one
        var prefix = BlobName("");
        if (prefix.Length > 0)
        {
            prefix = prefix.TrimEnd('/') + "/";
        }
        try
        {
            await foreach (var blob in _client.GetBlobsAsync(prefix: prefix))
            {
                output[blob.Name] = blob.Properties;
            }
        }
        catch (Exception ex)
        {
            // Not being able to list just means we upload everything like before
            _log.LogWarning(ex, "Couldn't list existing files under {Prefix} so every file will be uploaded", prefix);
            output.Clear();
        }
        return output;
    }

    public async Task WriteAllTextAsync(string path, string content)
    {
        await WriteAllBytesAsync(path, System.Text.Encoding.UTF8.GetBytes(content));
    }

    public Task WriteAllBytesAsync(string path, byte[] content)
    {
        _tasks.Add(UploadWithSemaphore(path, content, null));
        return Task.CompletedTask;
    }

    public Task WriteStreamAsync(string path, Stream stream)
    {
        _tasks.Add(UploadWithSemaphore(path, null, stream));
        return Task.CompletedTask;
    }

    public bool DirectoryExists(string path)
    {
        return true;
    }

    /// <summary>
    /// Storage drops a leading slash from blob names so do the same here, that way the listing prefix and the
    /// lookup keys match the names that come back from storage
    /// </summary>
    private string BlobName(string path)
    {
        return Path.Join(_basePath, path).Replace("\\", "/").TrimStart('/');
    }

    /// <summary>
    /// Uploads either the bytes or the stream. Only bytes can be compared against what's stored, streams always upload.
    /// </summary>
    private async Task UploadWithSemaphore(string path, byte[]? content, Stream? stream)
    {
        var extension = Path.GetExtension(path);
        var contentType = Utils.ExtensionsToMimeTypesMapping.GetValueOrDefault(extension, "application/octet-stream");
        var blobName = BlobName(path);
        var headers = new BlobHttpHeaders() { ContentType = contentType };
        if (content != null)
        {
            // Setting the hash ourselves means it is stored even when the SDK splits the upload into blocks
            headers.ContentHash = MD5.HashData(content);
            if (_existingBlobs != null)
            {
                var existing = await _existingBlobs;
                if (existing.TryGetValue(blobName, out var properties)
                    && properties.ContentType == contentType
                    && properties.ContentHash != null
                    && properties.ContentHash.AsSpan().SequenceEqual(headers.ContentHash))
                {
                    Interlocked.Increment(ref _skippedFiles);
                    return;
                }
            }
            // Wraps the array rather than copying it
            stream = new MemoryStream(content, false);
        }

        await _semaphore.WaitAsync();
        try
        {
            await _client.GetBlobClient(blobName).UploadAsync(stream, new BlobUploadOptions() { HttpHeaders = headers });
            Interlocked.Increment(ref _uploadedFiles);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// This doesn't do anything because Azure Blob Storage doesn't have directories.
    /// </summary>
    /// <param name="path"></param>
    public void CreateDirectory(string path)
    {
    }

    public string[] ListFilesInDirectory(string path, string pattern, SearchOption searchOption)
    {
        throw new NotImplementedException();
    }

    public Stream OpenRead(string path)
    {
        throw new NotImplementedException();
    }

    public async Task FinishAsync()
    {
        await Task.WhenAll(_tasks);
    }
}
