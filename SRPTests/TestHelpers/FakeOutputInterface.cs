using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PipelineCommon.Helpers;

namespace SRPTests.TestHelpers;

[ExcludeFromCodeCoverage]
public class FakeOutputInterface: IOutputInterface
{
    public Dictionary<string,string> Files = new();
    public List<string> Directories = new();
    
    public void Dispose()
    {
        // Nothing to dispose here
    }

    public void WriteAllText(string path, string content)
    {
        path = NormalizePath(path);
        if (Files.ContainsKey(path))
        {
            Files[path] = content;
        }
        Files.Add(path, content);
    }

    public Task WriteAllTextAsync(string path, string content)
    {
        path = NormalizePath(path);
        if (Files.ContainsKey(path))
        {
            Files[path] = content;
        }
        Files.Add(path, content);
        
        return Task.CompletedTask;
    }

    public Task WriteStreamAsync(string path, Stream stream)
    {
        throw new System.NotImplementedException();
    }

    public bool DirectoryExists(string path)
    {
        path = NormalizePath(path);
        return Directories.Contains(path);
    }

    public void CreateDirectory(string path)
    {
        path = NormalizePath(path);
        Directories.Add(path);
    }

    public string[] ListFilesInDirectory(string path, string pattern, SearchOption searchOption)
    {
        path = NormalizePath(path);
        if (searchOption == SearchOption.AllDirectories)
        {
            return Files.Keys.Where(f => f.StartsWith(path) && f.EndsWith(pattern)).ToArray();
        }
        return Files.Keys.Where(f =>
            f.StartsWith(path) && f.EndsWith(pattern) && f.Count(c => c == '/') == path.Count(c => c == '/')).ToArray();
    }

    public Stream OpenRead(string path)
    {
        path = NormalizePath(path);
        if (!Files.TryGetValue(path, out var content))
        {
            throw new FileNotFoundException();
        }

        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(content);
        stream.Position = 0;
        return stream;

    }

    public Task FinishAsync()
    {
        return Task.CompletedTask;
    }

    private string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
    
}