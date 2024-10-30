using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace PipelineCommon.Helpers;

public class DirectAzureUpload: IOutputInterface
{
    private readonly List<Task> _tasks = new();
    private readonly string _basePath;
    private readonly BlobContainerClient _client;
    private readonly SemaphoreSlim _semaphore;
    public DirectAzureUpload(string basePath, BlobContainerClient uploadClient, int maxConcurrentUploads = 50)
    {
        _basePath = basePath;
        _client = uploadClient;
        _semaphore = new SemaphoreSlim(maxConcurrentUploads);
    }
    public void Dispose()
    {
        _semaphore.Dispose();
    }

    public async Task WriteAllTextAsync(string path, string content)
    {
        await Upload(path, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
    }

    public async Task WriteStreamAsync(string path, Stream stream)
    {
        await Upload(path,stream);
    }

    public bool DirectoryExists(string path)
    {
        return true;
    }

    private async Task Upload(string path, Stream data)
    {
        var extension = Path.GetExtension(path);
        var contentType = Utils.ExtensionsToMimeTypesMapping.GetValueOrDefault(extension, "application/octet-stream");
        _tasks.Add(UploadWithSemaphore(data, path, contentType));
    }
    private async Task UploadWithSemaphore(Stream data, string path, string contentType)
    {
        await _semaphore.WaitAsync();
        try
        {
            var blobClient = _client.GetBlobClient(Path.Join(_basePath, path).Replace("\\", "/"));
            await blobClient.UploadAsync(data, new BlobUploadOptions() { HttpHeaders = new BlobHttpHeaders() { ContentType = contentType }});
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