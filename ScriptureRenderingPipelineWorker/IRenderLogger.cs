namespace ScriptureRenderingPipelineWorker;

public interface IRenderLogger
{
    public void LogWarning(string message);
    public void LogError(string message);
    /// <summary>
    /// Record a file that was written
    /// </summary>
    /// <param name="path">The path of the file</param>
    /// <param name="content">The UTF-8 bytes that were written</param>
    /// <param name="metadata">Optional extra information about the file such as the book</param>
    public void LogFile(string path, byte[] content, Dictionary<string, object>? metadata = null);
    public void LogTitle(string item, string title);
}