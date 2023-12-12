using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PipelineCommon.Helpers;

public class FileSystemOutputInterface : IOutputInterface
{
    private string BasePath { get; set; }
    public FileSystemOutputInterface(string basePath)
    {
        BasePath = basePath;
    }
    public void WriteAllText(string path, string content)
    {
        File.WriteAllText(Path.Join(BasePath, path), content);
    }
    public async Task WriteAllTextAsync(string path, string content)
    {
        await File.WriteAllTextAsync(Path.Join(BasePath, path), content);
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(Path.Join(BasePath, path));
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(Path.Join(BasePath, path));
    }
    
    public string[] ListFilesInDirectory(string path, string pattern, SearchOption searchOption)
    {
        return Directory.GetFiles(Path.Join(BasePath, path), pattern, searchOption).Select(i => Path.GetRelativePath(BasePath, i)).ToArray();
    }

    public Stream OpenRead(string path)
    {
        return File.OpenRead(Path.Join(BasePath, path));
    }

    public void Dispose()
    {
    }
    
    public Task FinishAsync()
    {
        return Task.CompletedTask;
    }
}