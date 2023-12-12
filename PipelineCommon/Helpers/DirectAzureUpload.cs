using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace PipelineCommon.Helpers;

public class DirectAzureUpload: IOutputInterface
{
    private List<Task> tasks = new();
    private string BasePath;
    private BlobContainerClient client = Utils.OutputClient;
    public DirectAzureUpload(string basePath)
    {
        BasePath = basePath;
    }
    public void Dispose()
    {
    }

    public void WriteAllText(string path, string content)
    {
        var extension = Path.GetExtension(path);
        var contentType = Utils.ExtensionsToMimeTypesMapping.GetValueOrDefault(extension, "application/octet-stream");
        var blobClient = client.GetBlobClient(Path.Join(BasePath, path).Replace("\\", "/"));
        tasks.Add(blobClient.UploadAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)),
            new BlobHttpHeaders() { ContentType = contentType }));
    }

    public Task WriteAllTextAsync(string path, string content)
    {
        WriteAllText(path,content);
        return Task.CompletedTask;
    }

    public bool DirectoryExists(string path)
    {
        return true;
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
        await Task.WhenAll(tasks);
    }
}