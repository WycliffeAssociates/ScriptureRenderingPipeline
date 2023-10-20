using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PipelineCommon.Helpers;

namespace SRPTests.TestHelpers;

public class FakeZipFileSystem: IZipFileSystem
{
    public bool IsOpen { get; set; }
    private Dictionary<string,string> Files { get; set; }
    private List<string> Folders { get; set; }
    private char Separator = '/';

    public void AddFile(string fileName, string fileContent)
    {
        Files.TryAdd(fileName, fileContent);
    }

    public void AddFolder(string folderName)
    {
        if (!Folders.Contains(folderName))
        {
            Folders.Add(folderName);
        }
    }
    

    public FakeZipFileSystem()
    {
        IsOpen = true;
    }
    
    public string ReadAllText(string file)
    {
        return Files[file];
    }

    public Task<string> ReadAllTextAsync(string file)
    {
        return Task.FromResult(Files[file]);
    }

    public IEnumerable<string> GetAllFiles(string pattern = null)
    {
        if (pattern == null)
        {
            return Files.Keys;
        }
        return Files.Keys.Where(x => x.EndsWith(pattern));
    }

    public IEnumerable<string> GetFiles(string baseDir, string pattern = null)
    {
        if (pattern == null)
        {
            return Files.Keys.Where(x => x.StartsWith(baseDir));
        }
        return Files.Keys.Where(x => x.StartsWith(baseDir) && x.EndsWith(pattern));
    }

    public bool FileExists(string path)
    {
        return Files.ContainsKey(path);
    }

    public string Join(params string[] input)
    {
        return NormalizePath(string.Join("/", input));
    }

    public string JoinPath(params string[] input)
    {
        return Join(input);
    }

    public IEnumerable<string> GetFolders(string path = null)
    {
        IEnumerable<string> output = Folders;
        if (path != null)
        {
            output = output.Where(e => e.StartsWith(path) && e.Length > path.Length).Select(s => s.Substring(path.TrimEnd(Separator).Length + 1)).Where( e => !string.IsNullOrEmpty(e));
        }

        // this is a request for the top level directories
        return output 
            .Select( e=> e.Split(Separator)[0])
            .Distinct();
    }

    public void Close()
    {
        IsOpen = false;
    }
    
    private string NormalizePath(string input)
    {
        //TODO: This should be modified to handle ".." also but that is slightly more difficult
        return string.Join(Separator,input.Split(Separator).Where(i => i != "."));
    }
}