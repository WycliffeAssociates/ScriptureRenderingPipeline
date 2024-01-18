using PipelineCommon.Models.BusMessages;

namespace ScriptureRenderingPipelineWorker;

public class FileTrackingLogger: IRenderLogger
{
    public FileTrackingLogger(string baseUrl)
    {
        BaseUrl = baseUrl;
    }
    private string BaseUrl { get; } 
    public List<string> Warnings { get; } = new();
    public List<string> Errors { get; } = new();
    public List<RenderedFile> Files { get; } = new();
    
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
        Files.Add(new RenderedFile()
        {
            Path = BaseUrl + path,
            Size = bytes.Length,
            FileType = Path.GetExtension(path).TrimStart('.'),
            Hash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(bytes))
        });
    }
}