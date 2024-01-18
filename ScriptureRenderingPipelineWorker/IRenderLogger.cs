namespace ScriptureRenderingPipelineWorker;

public interface IRenderLogger
{
    public void LogWarning(string message);
    public void LogError(string message);
    public void LogFile(string path, string content, Dictionary<string, object>? metadata = null);
}