using PipelineCommon.Helpers;

namespace ScriptureRenderingPipelineWorker;

/// <summary>
/// A wrapper for the output interface that also logs the output. This is something we'll do commonly so it makes sense to have a wrapper for it.
/// </summary>
public class OutputAndLoggingWrapper
{
    public OutputAndLoggingWrapper(IOutputInterface output, IRenderLogger? logger)
    {
        Output = output;
        Logger = logger;
    }
    public void LogWarning(string message)
    {
        Logger?.LogWarning(message);
    }
    
    public void LogError(string message)
    {
        Logger?.LogError(message);
    }
    public void LogTitle(string item, string title)
    {
        Logger?.LogTitle(item, title);
    }
    
    public async Task WriteAllTextAsync(string path, string content, Dictionary<string, object>? metadata = null)
    {
        Logger?.LogFile(path, content, metadata);
        await Output.WriteAllTextAsync(path, content);
    }

    private IOutputInterface Output { get; set; }
    private IRenderLogger? Logger { get; set; }

    public Task FinishAsync()
    {
        return Output.FinishAsync();
    }
}