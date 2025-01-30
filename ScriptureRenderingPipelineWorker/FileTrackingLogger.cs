using System.Text.RegularExpressions;
using PipelineCommon.Helpers;
using PipelineCommon.Models.BusMessages;

namespace ScriptureRenderingPipelineWorker;

public class FileTrackingLogger: IRenderLogger
{
    private readonly RepoType _type;
    public string BaseUrl { get; } 
    public List<string> Warnings { get; } = new();
    public List<string> Errors { get; } = new();
    public List<RenderedFile> Files { get; } = new();
    public Dictionary<string,string> Titles { get; } = new();
    
    public FileTrackingLogger(string baseUrl, RepoType type)
    {
        BaseUrl = baseUrl;
        _type = type;
    }
    
    public void LogWarning(string message)
    {
        Warnings.Add(message);
    }

    public void LogError(string message)
    {
        Errors.Add(message);
    }

    public void LogFile(string path, string content, Dictionary<string, object>? metadata = null)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var tmp = new RenderedFile()
        {
            Path = $"/{path}".Replace("\\", "/"),
            Size = bytes.Length,
            FileType = Path.GetExtension(path).TrimStart('.'),
            Hash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(bytes))
        };
        if (metadata != null)
        {
            if (metadata.TryGetValue("book", out var book))
            {
                tmp.Slug = book as string;
            }
        }
        AddMetadataToFileEntry(path, tmp);
        Files.Add(tmp);
    }

    public void LogTitle(string item, string title)
    {
        if (!Titles.TryAdd(item, title))
        {
            Titles[item] = title;
        }
    }

    private void AddMetadataToFileEntry(string path, RenderedFile tmp)
    {
        switch (_type)
        {
            case RepoType.Bible:
            case RepoType.translationNotes:
            case RepoType.translationQuestions:
            case RepoType.BibleCommentary:
                var pathSplit = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (pathSplit.Length == 2)
                {
                    tmp.Book ??= pathSplit[0];
                    tmp.Chapter ??= GetChapterNumberFromPath(pathSplit);
                }
                break;
            case RepoType.translationWords:
            case RepoType.translationAcademy:
                tmp.Slug = GetSlugFromPath(path);
                break;

            case RepoType.Unknown:
            case RepoType.OpenBibleStories:
            default:
                break;
        }
    }

    private static int? GetChapterNumberFromPath(string[] pathSplit)
    {
        if (pathSplit[1].EndsWith(".html"))
        {
            if (int.TryParse(pathSplit[1].AsSpan()[..^5], out var chapter))
            {
                return chapter;
            }
        }

        return null;
    }

    private static string? GetSlugFromPath(string path)
    {
        if (path.EndsWith(".html") && path != "print_all.html")
        {
            return Path.GetFileNameWithoutExtension(path);
        }

        return null;
    }
}