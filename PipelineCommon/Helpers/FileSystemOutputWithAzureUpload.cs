using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;

namespace PipelineCommon.Helpers;

public class FileSystemOutputWithAzureUpload : IOutputInterface
{
    private string FileSystemBasePath { get; set; }
    private string OutputPath { get; set; }
    private ILogger Log;
    public FileSystemOutputWithAzureUpload(string basePath, string outputPath, ILogger logger, bool uploadWhenCreated = false)
    {
        FileSystemBasePath = basePath;
        OutputPath = outputPath;
        Log = logger;
    }
    public void WriteAllText(string path, string content)
    {
        File.WriteAllText(Path.Join(FileSystemBasePath, path), content);
    }
    public async Task WriteAllTextAsync(string path, string content)
    {
        await File.WriteAllTextAsync(Path.Join(FileSystemBasePath, path), content);
    }

    public async Task WriteStreamAsync(string path, Stream stream)
    {
        var file = File.OpenWrite(path);
        await stream.CopyToAsync(file);
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(Path.Join(FileSystemBasePath, path));
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(Path.Join(FileSystemBasePath, path));
    }
    
    public string[] ListFilesInDirectory(string path, string pattern, SearchOption searchOption)
    {
        return Directory.GetFiles(Path.Join(FileSystemBasePath, path), pattern, searchOption).Select(i => Path.GetRelativePath(FileSystemBasePath, i)).ToArray();
    }

    public Stream OpenRead(string path)
    {
        return File.OpenRead(Path.Join(FileSystemBasePath, path));
    }

    public void Dispose()
    {
        Directory.Delete(FileSystemBasePath, true);
    }

    public async Task FinishAsync()
    {
        await UploadToAzureStorage();
    }

    private async Task UploadToAzureStorage()
    {
        await Utils.GetOutputClient().CreateIfNotExistsAsync();
        var uploadTasks = new List<Task>();
        foreach (var file in Directory.GetFiles(FileSystemBasePath, "*.*", SearchOption.AllDirectories))
        {
            var fileRelativePath = Path.GetRelativePath(FileSystemBasePath, file);
            var extension = Path.GetExtension(file);
            Log.LogDebug("Uploading {Path}", fileRelativePath);
            var tmp = Utils.GetOutputClient().GetBlobClient(Path.Join(OutputPath, fileRelativePath).Replace("\\", "/"));
            var contentType = Utils.ExtensionsToMimeTypesMapping.GetValueOrDefault(extension, "application/octet-stream");
            uploadTasks.Add(Task.Run(async ()=>
            {
                await using var content = OpenRead(file);
                await tmp.UploadAsync(content,
                    new BlobUploadOptions() { HttpHeaders = new BlobHttpHeaders() { ContentType = contentType } });
            }));
        }
        await Task.WhenAll(uploadTasks);
    }
}