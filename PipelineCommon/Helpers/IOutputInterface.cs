using System;
using System.IO;
using System.Threading.Tasks;

namespace PipelineCommon.Helpers;

public interface IOutputInterface: IDisposable
{
    Task WriteAllTextAsync(string path, string content);
    Task WriteStreamAsync(string path, Stream stream);

    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    string[] ListFilesInDirectory(string path, string pattern, SearchOption searchOption);
    Stream OpenRead(string path);
    Task FinishAsync();
}