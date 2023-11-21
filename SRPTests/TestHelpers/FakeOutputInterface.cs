using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PipelineCommon.Helpers;

namespace SRPTests.TestHelpers;

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
        if (Files.ContainsKey(path))
        {
            Files[path] = content;
        }
        Files.Add(path, content);
    }

    public Task WriteAllTextAsync(string path, string content)
    {
        if (Files.ContainsKey(path))
        {
            Files[path] = content;
        }
        Files.Add(path, content);
        
        return Task.CompletedTask;
    }

    public bool DirectoryExists(string path)
    {
        return Directories.Contains(path);
    }

    public void DeleteDirectory(string path)
    {
        Directories.Remove(path);
    }

    public void CreateDirectory(string path)
    {
        Directories.Add(path);
    }

    public string[] ListFilesInDirectory(string path)
    {
        return Files.Keys.Where(f => f.StartsWith(path)).ToArray();
    }

    public string[] ListFilesInDirectory(string path, string pattern)
    {
        return Files.Keys.Where(f =>
            f.StartsWith(path) && f.EndsWith(pattern) && f.Count(c => c == '/') == path.Count(c => c == '/')).ToArray();
    }

    public string[] ListFilesInDirectory(string path, string pattern, SearchOption searchOption)
    {
        if (searchOption == SearchOption.AllDirectories)
        {
            return Files.Keys.Where(f => f.StartsWith(path) && f.EndsWith(pattern)).ToArray();
        }
        return Files.Keys.Where(f =>
            f.StartsWith(path) && f.EndsWith(pattern) && f.Count(c => c == '/') == path.Count(c => c == '/')).ToArray();
    }

    public string GetRelativePath(string path)
    {
        return path;
    }

    public Stream OpenRead(string path)
    {
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
}